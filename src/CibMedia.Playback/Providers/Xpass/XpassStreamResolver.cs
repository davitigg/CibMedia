using CibMedia.Playback.Logging;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using CibMedia.Playback.Models;
using CibMedia.Playback.Providers.Xpass.Models;

namespace CibMedia.Playback.Providers.Xpass;

// Turns the server list into streams a plain HLS client can play. Most xpass servers hide MPEG-TS
// inside PNG bodies on TikTok CDNs and rely on the embed's service worker to strip the prefix;
// those cannot play outside the embed, so a candidate's first segment is read before the server is
// offered.
internal sealed class XpassStreamResolver(HttpClient http, XpassOptions options, ILogger<XpassStreamResolver> logger)
{
    private const byte TransportStreamSync = 0x47;
    private const int TransportStreamPacket = 188;

    // Two consecutive sync bytes rule out a text body that happens to start with 'G'.
    private const int TransportStreamProbeLength = TransportStreamPacket * 2 + 1;

    // Every candidate costs a playlist lookup against the player origin, which answers a burst of
    // roughly twenty requests with a 429. Everything that verifies is offered; nothing unverified
    // is appended behind it.
    private const int MaxCandidates = 5;

    // Verifying a server is four sequential hops: playlist, master, variant, segment. Candidates
    // run as one concurrent wave, so the wave deadline is the ceiling a cold miss can add to the
    // response. The hop timeout is what stops a host that never answers from holding the wave at
    // that ceiling on its own. It must clear the slowest hop a good server makes: a LUL master on
    // a cold Cloudflare worker measures ~1.25s alone and ~1.5s inside the wave, and a tighter
    // timeout was found to drop those servers rather than the hanging ones.
    private static readonly TimeSpan HopTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan WaveDeadline = TimeSpan.FromSeconds(3);

    // Ordering only; correctness comes from the byte check. Names seen serving clean HLS go first,
    // names seen serving PNG-wrapped HLS last. The MP4-only classes (BOX, BIG, MIX, ZUR) are left
    // out on purpose: nothing is built from them, so promoting one wastes a probe slot.
    private static readonly string[] CleanPrefixes = ["VIP", "LUL", "WIS"];
    private static readonly string[] WrappedPrefixes = ["TIK", "FIL", "AKC", "MEG"];

    private readonly Uri origin = new($"https://{options.PlayerHost}/");

    public async Task<IReadOnlyList<PlaybackStream>> ProbeAsync(
        IReadOnlyList<XpassServer> servers,
        CancellationToken cancellationToken
    )
    {
        // OrderBy is stable, so ties keep the embed's own order.
        var candidates = servers
            .Where(server => !string.IsNullOrWhiteSpace(server.Url))
            .OrderBy(Rank)
            .Take(MaxCandidates)
            .Select((server, index) => (Server: server, Label: LabelFor(server, index)));

        using var wave = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        wave.CancelAfter(WaveDeadline);

        var verified = await Task.WhenAll(
            candidates.Select(candidate => VerifyAsync(candidate.Server, candidate.Label, wave.Token)));

        return verified.OfType<PlaybackStream>().ToList();
    }

    private static int Rank(XpassServer server)
    {
        var name = server.Name ?? "";

        if (CleanPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))) return 0;
        if (WrappedPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))) return 2;

        return 1;
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
            if (!cancellationToken.IsCancellationRequested)
                logger.XpassServerUnverified(server.Name, exception);

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

        return source is null || IsProgressive(source) ? null : source.File;
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

        return head.Length > TransportStreamPacket
               && head[0] == TransportStreamSync
               && head[TransportStreamPacket] == TransportStreamSync;
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

    private HttpRequestMessage NewRequest(Uri uri)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Referrer = origin;

        return request;
    }
}