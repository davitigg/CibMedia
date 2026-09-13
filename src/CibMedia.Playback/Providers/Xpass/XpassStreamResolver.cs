using CibMedia.Playback.Logging;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using CibMedia.Playback.Models;
using CibMedia.Playback.Providers.Xpass.Models;

namespace CibMedia.Playback.Providers.Xpass;

// Turns the server list into streams the head can play. Many xpass servers append the MPEG-TS to a
// complete 1x1 PNG and rely on the embed's service worker to strip it; the head's
// PngWrappedDataSource does the same, so a wrapped segment counts as carried. What is read is the
// candidate's first segment either way — a server offering neither shape is not offered.
internal sealed class XpassStreamResolver(HttpClient http, XpassOptions options, ILogger<XpassStreamResolver> logger)
{
    private const byte TransportStreamSync = 0x47;
    private const int TransportStreamPacket = 188;

    // Two consecutive sync bytes rule out a text body that happens to start with 'G'. The wrapper
    // is read on top of them: the longest measured runs to 941 bytes, and the allowance is well
    // clear of it — a wrapper this does not reach the end of costs the server its place.
    private const int PrefixAllowance = 4096;
    private const int TransportStreamProbeLength = PrefixAllowance + TransportStreamPacket * 2 + 1;

    // Every candidate costs a playlist lookup against the player origin, which answers a burst of
    // roughly twenty requests with a 429. Everything that verifies is offered; nothing unverified
    // is appended behind it.
    private const int MaxCandidates = 12;

    // How many probes run at once. A replacement starts the moment one lands rather than when the
    // whole group does, so one slow candidate costs its own place and nobody else's.
    private const int InFlight = 4;

    // A prefix names a CDN family, never an encode: one measured episode had LUL 1-4 and LUL 8 at
    // 404 while LUL 5-7 played. Capping a family in flight is what stops the eight LUL entries
    // holding every slot on one guess; a candidate passed over keeps its place for the moment one
    // of its siblings lands.
    private const int PerFamilyInFlight = 2;

    // What the Streams picker is offered: two fallbacks behind the one that plays, which is as far
    // as a sitting ever gets. Nothing is started once this many have verified or are one probe from
    // it, which is what keeps a title off the candidates behind them.
    private const int EnoughStreams = 3;

    // Verifying a server is four sequential hops: playlist, master, variant, segment. This is what
    // stops a host that never answers from spending the whole probe on its own, and it is measured
    // on the box rather than on a desktop: a LUL master on a cold Cloudflare worker clears two
    // seconds from a desktop but not from the box, where at that budget every LUL server failed a
    // hop and xpass offered the fastest server alone. At four the same title offers four.
    private static readonly TimeSpan HopTimeout = TimeSpan.FromSeconds(4);

    // The whole probe, and the only deadline above the hop: a candidate is now dropped when its own
    // hops run out rather than when a group it happened to share is cut, so there is no second
    // deadline discarding work this one still has room for. Twelve seconds is three full turns of
    // the pool on the box, which measured at 6-11s for a title that answers at all.
    private static readonly TimeSpan ProbeDeadline = TimeSpan.FromSeconds(12);

    // Ordering only; correctness comes from the byte check. Set from ten titles rather than
    // guessed — VIP carried 9 of 10, TIK 7 of 9, LUL 26 of 42, WIS 10 of 21, against FIL at 4 of 26
    // and MOL at 2 of 14. TIK is PNG-wrapped, which is worth a tier now that the head strips one.
    //
    // The embed's own order was measured against this and cost 6.6 probes a title to this table's
    // 4.6: it lists the same families in the same places whatever the title, so being first says
    // nothing about holding this one. Finer tiers than these two were measured and gained nothing.
    private static readonly string[] CleanPrefixes = ["VIP", "TIK", "LUL", "WIS"];

    // Nothing in 31 probes over nine titles, and every one of them spent a whole hop timing out
    // rather than failing fast. Behind the names never seen at all: the cost of trying one is the
    // worst there is, and MaxCandidates means a title with any depth never reaches them.
    private static readonly string[] SlowPrefixes = ["ARA", "SAF"];

    // What a server answers with for a title it does not hold. Free to spot, and spotting it saves
    // the three hops that would chase it.
    private const string MissingFile = "/video/error";

    private readonly Uri origin = new($"https://{options.PlayerHost}/");

    public async Task<IReadOnlyList<PlaybackStream>> ProbeAsync(
        IReadOnlyList<XpassServer> servers,
        CancellationToken cancellationToken
    )
    {
        // OrderBy is stable, so ties keep the embed's own order, and the place a candidate takes
        // here is the place its stream takes in the answer.
        var candidates = servers
            .Where(server => !string.IsNullOrWhiteSpace(server.Url))
            .Select((server, index) => (Server: server, Label: LabelFor(server, index)))
            .OrderBy(entry => Rank(entry.Server))
            .Take(MaxCandidates)
            .Select((entry, place) => new Candidate(entry.Server, entry.Label, place))
            .ToList();

        using var probe = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        probe.CancelAfter(ProbeDeadline);

        var pending = new LinkedList<Candidate>(candidates);
        var running = new List<Task<Probe>>();
        var families = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var verified = new SortedDictionary<int, PlaybackStream>();

        try
        {
            while (true)
            {
                Fill(pending, running, families, verified.Count, probe.Token);

                if (running.Count is 0) break;

                var finished = await Task.WhenAny(running);
                running.Remove(finished);

                var probed = await finished;

                families[Family(probed.Candidate.Server.Name)] -= 1;

                if (probed.Stream is { } stream) verified[probed.Candidate.Place] = stream;

                if (verified.Count >= EnoughStreams || probe.IsCancellationRequested) break;
            }
        }
        finally
        {
            await probe.CancelAsync();

            // Awaited so nothing still holds a token from a source about to go out of scope. The
            // faults are dropped: throwing from here would replace the answer, or the 429 that
            // ended it, with whatever a probe nobody is waiting for hit on its way out.
            await ((Task)Task.WhenAll(running)).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }

        return verified.Values.Take(EnoughStreams).ToList();
    }

    // Tops the pool back up in ranked order, passing over a candidate whose family already has its
    // share in flight. Counting what has verified alongside what is running is what stops a probe
    // being started for a place the answer is already going to fill.
    private void Fill(
        LinkedList<Candidate> pending,
        List<Task<Probe>> running,
        Dictionary<string, int> families,
        int verified,
        CancellationToken cancellationToken
    )
    {
        if (cancellationToken.IsCancellationRequested) return;

        for (var node = pending.First; node is not null && verified + running.Count < InFlight;)
        {
            var next = node.Next;
            var family = Family(node.Value.Server.Name);

            if (families.GetValueOrDefault(family) < PerFamilyInFlight)
            {
                families[family] = families.GetValueOrDefault(family) + 1;
                running.Add(VerifyAsync(node.Value, cancellationToken));
                pending.Remove(node);
            }

            node = next;
        }
    }

    // "LUL 3" and "LUL 7" are one family; the number is the entry inside it.
    private static string Family(string? name)
    {
        var trimmed = (name ?? "").Trim();
        var space = trimmed.IndexOf(' ');

        return space < 0 ? trimmed : trimmed[..space];
    }

    private static int Rank(XpassServer server)
    {
        if (HasPrefix(server.Name, CleanPrefixes)) return 0;
        if (HasPrefix(server.Name, SlowPrefixes)) return 2;

        return 1;
    }

    private static bool HasPrefix(string? name, string[] prefixes)
    {
        return prefixes.Any(prefix => (name ?? "").StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    // The embed's own server menu shows these names, so the player shows the same ones.
    private static string LabelFor(XpassServer server, int index)
    {
        return string.IsNullOrWhiteSpace(server.Name) ? $"Server {index + 1}" : server.Name.Trim();
    }

    // Answers with its candidate whatever the outcome, so the pool can put the family's slot back
    // without holding a second structure to map a task to the server that started it.
    private async Task<Probe> VerifyAsync(Candidate candidate, CancellationToken cancellationToken)
    {
        try
        {
            var master = await ResolveMasterAsync(candidate.Server.Url!, cancellationToken);

            return new Probe(
                candidate,
                master is not null && await CarriesTransportStreamAsync(master, cancellationToken)
                    ? new HlsStream(master, candidate.Label)
                    : null);
        }
        catch (Exception exception) when (IsServerFault(exception))
        {
            // The deadline arriving is not the server's fault; a hop timing out is.
            if (cancellationToken.IsCancellationRequested) logger.XpassServerUnfinished(candidate.Server.Name);
            else logger.XpassServerUnverified(candidate.Server.Name, exception);

            return new Probe(candidate, null);
        }
    }

    // A 429 is the origin refusing the burst, not this server failing to verify, so it is left to
    // propagate: swallowed, it would report the title as sourceless and have that cached as a miss.
    private static bool IsServerFault(Exception exception)
    {
        return exception is HttpRequestException { StatusCode: not HttpStatusCode.TooManyRequests }
            or OperationCanceledException
            or JsonException
            or UriFormatException;
    }

    // The server's HLS master URL, or null when it offers none or offers a progressive file. MP4
    // servers are skipped: upstream declares an audio language nowhere, and no measured title
    // resolved through one without also resolving through HLS.
    private async Task<string?> ResolveMasterAsync(string relativeUrl, CancellationToken cancellationToken)
    {
        var body = (await GetStringAsync(new Uri(origin, relativeUrl), cancellationToken)).TrimStart();
        if (body.Length is 0) return null;

        // A server answers either with a bare item array or with it wrapped under "playlist".
        var items = body[0] is '['
            ? JsonSerializer.Deserialize<List<XpassPlaylistItem>>(body, JsonSerializerOptions.Web)
            : JsonSerializer.Deserialize<XpassPlaylistDocument>(body, JsonSerializerOptions.Web)?.Playlist;

        var first = items is { Count: > 0 } ? items[0] : null;

        var source = first?.Sources?.FirstOrDefault(entry => !string.IsNullOrWhiteSpace(entry.File));

        return source is null || IsMissing(source) || IsProgressive(source) ? null : source.File;
    }

    private static bool IsMissing(XpassSource source)
    {
        return source.File!.EndsWith(MissingFile, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProgressive(XpassSource source)
    {
        return string.Equals(source.Type, "mp4", StringComparison.OrdinalIgnoreCase)
               || source.File!.Contains(".mp4", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> CarriesTransportStreamAsync(string masterUrl, CancellationToken cancellationToken)
    {
        var masterUri = new Uri(masterUrl);
        var master = await GetStringAsync(masterUri, cancellationToken);

        // A master lists variants; a media playlist lists segments. Either way the first URI leads on.
        var mediaUri = masterUri;
        var media = master;
        if (master.Contains("#EXT-X-STREAM-INF", StringComparison.Ordinal))
        {
            var variant = FirstUri(master);
            if (variant is null) return false;

            mediaUri = new Uri(masterUri, variant);
            media = await GetStringAsync(mediaUri, cancellationToken);
        }

        var segment = FirstUri(media);
        if (segment is null) return false;

        var head = await ReadPrefixAsync(new Uri(mediaUri, segment), TransportStreamProbeLength, cancellationToken);

        return CarriesTransportStream(head);
    }

    private static bool CarriesTransportStream(ReadOnlySpan<byte> head)
    {
        var payload = head;

        if (PngWrappedSegment.Opens(head))
        {
            var start = PngWrappedSegment.PayloadStart(head);

            // A body that opened like a PNG and closed on nothing is not a shape the head can
            // strip either.
            if (start < 0) return false;

            payload = head[start..];
        }

        return payload.Length > TransportStreamPacket
               && payload[0] == TransportStreamSync
               && payload[TransportStreamPacket] == TransportStreamSync;
    }

    private static string? FirstUri(string playlist)
    {
        foreach (var raw in playlist.AsSpan().EnumerateLines())
        {
            var line = raw.Trim();
            if (line.Length > 0 && line[0] != '#') return line.ToString();
        }

        return null;
    }

    private async Task<string> GetStringAsync(Uri uri, CancellationToken cancellationToken)
    {
        using var hop = StartHop(cancellationToken);
        using var request = NewRequest(uri);
        using var response = await http.SendAsync(request, hop.Token);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync(hop.Token);
    }

    // Reads only the leading bytes: segments run to hundreds of kilobytes and the verdict is in the
    // first few. Range is advisory, so the body is abandoned rather than drained.
    private async Task<byte[]> ReadPrefixAsync(Uri uri, int length, CancellationToken cancellationToken)
    {
        using var hop = StartHop(cancellationToken);
        using var request = NewRequest(uri);
        request.Headers.Range = new RangeHeaderValue(0, length - 1);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, hop.Token);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(hop.Token);
        var buffer = new byte[length];
        var read = await stream.ReadAtLeastAsync(buffer, length, false, hop.Token);

        return read == length ? buffer : buffer[..read];
    }

    private static CancellationTokenSource StartHop(CancellationToken cancellationToken)
    {
        var hop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        hop.CancelAfter(HopTimeout);

        return hop;
    }

    // Place is where the candidate ranked, which is the order the streams are offered in however
    // the probes happened to land.
    private sealed record Candidate(XpassServer Server, string Label, int Place);

    private sealed record Probe(Candidate Candidate, PlaybackStream? Stream);

    private HttpRequestMessage NewRequest(Uri uri)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Referrer = origin;

        return request;
    }
}
