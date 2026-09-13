using CibMedia.Playback.Extensions;
using CibMedia.Playback.Logging;
using CibMedia.Playback;
using CibMedia.Playback.Models;
using CibMedia.Playback.Providers.Xpass.Models;
using Microsoft.Extensions.Caching.Memory;

namespace CibMedia.Playback.Providers.Xpass;

// The xpass stack. Video and subtitles both come from its own hosts, so the pair is timed against
// the same encode by construction.
internal sealed class XpassPlaybackProvider(
    XpassClient client,
    XpassStreamResolver resolver,
    XpassSubtitleClient subtitles,
    XpassOptions options,
    IMemoryCache cache,
    ILogger<XpassPlaybackProvider> logger
) : ITmdbPlaybackProvider
{
    // The player origin rate-limits with a 429, so backing off is what stops a burst from feeding
    // itself. Global rather than per-title: the limit is per client, not per path.
    private const string OutageKey = "playback:xpass:unavailable";

    public PlaybackProvider Provider => PlaybackProvider.Xpass;

    public bool Enabled => options.Enabled;

    public Task<PlaybackSource?> GetMovieAsync(int tmdbId, CancellationToken cancellationToken)
    {
        return BuildSourceAsync($"movie/{tmdbId}", cancellationToken);
    }

    public Task<PlaybackSource?> GetEpisodeAsync(
        int tmdbId,
        int season,
        int episode,
        CancellationToken cancellationToken
    )
    {
        return BuildSourceAsync($"tv/{tmdbId}/{season}/{episode}", cancellationToken);
    }

    private async Task<PlaybackSource?> BuildSourceAsync(string path, CancellationToken cancellationToken)
    {
        // The probe is the slow part, so its verdict is what gets cached, not the server list.
        var resolved = await cache.GetOrStoreAsync(
            $"playback:xpass:{path}",
            async token => await ResolveAsync(path, token),
            Lifetime,
            cancellationToken);

        if (resolved.Streams.Count is 0) return null;

        // Kept for every stream: the CDNs differ, and one of the four measured refuses playback
        // without the embed's Referer and a browser agent even though the rest play bare.
        var headers = new Dictionary<string, string>
        {
            ["Referer"] = $"https://{options.PlayerHost}/",
            ["User-Agent"] = BrowserUserAgent.Value
        };

        return new PlaybackSource(
            Provider,
            headers,
            resolved.Streams,
            resolved.Subtitles);
    }

    // Servers offered and none of them verified is not the catalogue saying no, and it is the one
    // answer here that a bad few minutes upstream produces on its own.
    private static TimeSpan Lifetime(XpassResolved resolved)
    {
        return resolved is { ServersOffered: > 0, Streams.Count: 0 }
            ? ITmdbPlaybackProvider.UnverifiedTtl
            : ITmdbPlaybackProvider.AnswerTtl;
    }

    // Called from inside the cache factory: a hit never reaches upstream, so the flag costs nothing.
    private async Task<XpassResolved> ResolveAsync(string path, CancellationToken cancellationToken)
    {
        // Throws rather than returns empty: an empty result would be cached and read as "not in
        // catalogue" rather than "throttled".
        if (cache.TryGetValue(OutageKey, out _)) throw new PlaybackOutageException(Provider);

        // Subtitles do not depend on which server wins, so they ride alongside the probe. Started
        // outside the try because it answers for itself: a fault here is never the video's.
        var subtitlesTask = ReadSubtitlesAsync(path, cancellationToken);

        try
        {
            var servers = await client.GetServersAsync(path, cancellationToken);
            var streams = servers.Count is 0 ? [] : await resolver.ProbeAsync(servers, cancellationToken);
            var tracks = await subtitlesTask;

            // The count is the only record that the stack had something to offer and none of it
            // held up; downstream this answer is indistinguishable from a title nobody carries.
            if (servers.Count > 0 && streams.Count is 0) logger.XpassNothingVerified(path, servers.Count);

            // A source carrying subtitles without video would invite pairing them with another
            // stack's encode, which does not line up.
            return new XpassResolved(servers.Count, streams, streams.Count is 0 ? [] : tracks);
        }
        catch (Exception exception) when (exception.IsUpstreamFault(cancellationToken))
        {
            var window = ITmdbPlaybackProvider.OutageFor(exception);

            cache.Set(OutageKey, true, window);

            logger.ProviderOutageOpened(Provider, window, exception);

            throw;
        }
    }

    // The subtitle host is a different origin from the player and carries an extra rather than the
    // source, so its outage must not open the window that skips this stack's video. Seen down on
    // its own for hours while the player answered throughout.
    private async Task<IReadOnlyList<PlaybackSubtitle>> ReadSubtitlesAsync(
        string path,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await subtitles.GetAsync(path, cancellationToken);
        }
        catch (Exception exception) when (exception.IsUpstreamFault(cancellationToken))
        {
            logger.XpassSubtitlesUnavailable(exception);

            return [];
        }
    }
}