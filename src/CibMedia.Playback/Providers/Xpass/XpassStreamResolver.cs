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

    // A prefix names a CDN family, never an encode: one measured episode had LUL 1-4 and LUL 8 at
    // 404 while LUL 5-7 played. So candidates go in waves rather than in one take, and a family is
    // capped inside a wave — otherwise the eight LUL entries spend the whole of it on one guess.
    private const int WaveSize = 4;
    private const int PerFamilyPerWave = 2;

    // What the Streams picker is offered: two fallbacks behind the one that plays, which is as far
    // as a sitting ever gets. Stopping here is what keeps a title off the later waves.
    private const int EnoughStreams = 3;

    // Verifying a server is four sequential hops: playlist, master, variant, segment. A wave runs
    // its candidates concurrently, so the wave deadline is the ceiling one adds to the response.
    // The hop timeout is what stops a host that never answers from holding the wave at that
    // ceiling on its own, and it is measured on the box rather than on a desktop: a LUL master on
    // a cold Cloudflare worker clears two seconds from a desktop but not from the box, where at
    // that budget every LUL server failed a hop and xpass offered the fastest server alone. At
    // four the same title offers four.
    private static readonly TimeSpan HopTimeout = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan WaveDeadline = TimeSpan.FromSeconds(6);

    // Only a title the earlier waves left short reaches a later one, and this is what stops that
    // title from costing every wave's deadline in turn. Two full waves and most of a third: the
    // box resolves a cold title in around three times what a desktop does — 11s measured against
    // 3.5s — so at twelve a third wave could not start, and the titles that need one are the
    // titles that otherwise carry nothing.
    private static readonly TimeSpan ProbeDeadline = TimeSpan.FromSeconds(15);

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
        // OrderBy is stable, so ties keep the embed's own order.
        var candidates = servers
            .Where(server => !string.IsNullOrWhiteSpace(server.Url))
            .Select((server, index) => new Candidate(server, LabelFor(server, index)))
            .OrderBy(candidate => Rank(candidate.Server))
            .Take(MaxCandidates)
            .ToList();

        using var probe = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        probe.CancelAfter(ProbeDeadline);

        var streams = new List<PlaybackStream>();

        foreach (var wave in Waves(candidates))
        {
            if (probe.IsCancellationRequested) break;

            streams.AddRange(await RunWaveAsync(wave, probe.Token));

            if (streams.Count >= EnoughStreams) break;
        }

        // A wave answers as a whole, so the one that reaches the count can carry past it.
        return streams.Count > EnoughStreams ? streams[..EnoughStreams] : streams;
    }

    private async Task<IReadOnlyList<PlaybackStream>> RunWaveAsync(
        IReadOnlyList<Candidate> wave,
        CancellationToken cancellationToken
    )
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(WaveDeadline);

        var verified = await Task.WhenAll(
            wave.Select(candidate => VerifyAsync(candidate.Server, candidate.Label, deadline.Token)));

        return verified.OfType<PlaybackStream>().ToList();
    }

    // Walks the ranked order filling one wave at a time, passing over a candidate whose family is
    // already at its cap for that wave — so it keeps its place and lands in the next one.
    private static IEnumerable<List<Candidate>> Waves(IReadOnlyList<Candidate> candidates)
    {
        var pending = new LinkedList<Candidate>(candidates);

        while (pending.Count > 0)
        {
            var wave = new List<Candidate>(WaveSize);
            var taken = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (var node = pending.First; node is not null && wave.Count < WaveSize;)
            {
                var next = node.Next;
                var family = Family(node.Value.Server.Name);

                if (taken.GetValueOrDefault(family) < PerFamilyPerWave)
                {
                    taken[family] = taken.GetValueOrDefault(family) + 1;
                    wave.Add(node.Value);
                    pending.Remove(node);
                }

                node = next;
            }

            yield return wave;
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

    private async Task<PlaybackStream?> VerifyAsync(
        XpassServer server,
        string label,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var master = await ResolveMasterAsync(server.Url!, cancellationToken);

            return master is not null && await CarriesTransportStreamAsync(master, cancellationToken)
                ? new HlsStream(master, label)
                : null;
        }
        catch (Exception exception) when (IsServerFault(exception))
        {
            // The wave ending is not the server's fault; a hop timing out is.
            if (cancellationToken.IsCancellationRequested) logger.XpassServerUnfinished(server.Name);
            else logger.XpassServerUnverified(server.Name, exception);

            return null;
        }
    }

    // A 429 is the origin refusing the wave, not this server failing to verify, so it is left to
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

    private sealed record Candidate(XpassServer Server, string Label);

    private HttpRequestMessage NewRequest(Uri uri)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Referrer = origin;

        return request;
    }
}