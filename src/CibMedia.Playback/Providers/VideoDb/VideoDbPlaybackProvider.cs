using CibMedia.Playback.Extensions;
using CibMedia.Playback.Logging;
using System.Text.RegularExpressions;
using CibMedia.Playback;
using CibMedia.Playback.Models;
using CibMedia.Playback.Providers.VideoDb.Models;
using Microsoft.Extensions.Caching.Memory;

namespace CibMedia.Playback.Providers.VideoDb;

internal sealed partial class VideoDbPlaybackProvider(
    VideoDbClient client,
    VideoDbOptions options,
    IMemoryCache cache,
    ILogger<VideoDbPlaybackProvider> logger
) : ITmdbPlaybackProvider
{
    // The client only throws once every mirror is down, so the outage is global, not per-title.
    private const string OutageKey = "playback:videodb:unavailable";

    public PlaybackProvider Provider => PlaybackProvider.VideoDb;

    public bool Enabled => options.Enabled;

    public async Task<PlaybackSource?> GetMovieAsync(int tmdbId, CancellationToken cancellationToken)
    {
        var playlist = await GetPlaylistAsync(VideoDbContentType.Movie, tmdbId, cancellationToken);

        return playlist is null ? null : BuildSource(playlist.Host, playlist.Entries[0]);
    }

    public async Task<PlaybackSource?> GetEpisodeAsync(
        int tmdbId,
        int season,
        int episode,
        CancellationToken cancellationToken
    )
    {
        var playlist = await GetPlaylistAsync(VideoDbContentType.Serial, tmdbId, cancellationToken);

        var node = playlist is null
            ? null
            : EnumerateEpisodes(playlist.Entries)
                .FirstOrDefault(candidate => candidate.Season == season && candidate.Episode == episode);

        return node is null ? null : BuildSource(playlist!.Host, node.Entry);
    }

    // One upstream call carries the entire season tree, so every episode of a series is served from
    // a single cache entry.
    private async Task<VideoDbPlaylist?> GetPlaylistAsync(
        VideoDbContentType type,
        int tmdbId,
        CancellationToken cancellationToken
    )
    {
        return await cache.GetOrStoreAsync(
            $"playback:videodb:{type}:{tmdbId}",
            async token => await FetchAsync(type, tmdbId, token),
            // A mirror answering "not in catalogue" is upstream stating a fact about itself, so a
            // null here is worth exactly as long as a playlist is.
            _ => ITmdbPlaybackProvider.AnswerTtl,
            cancellationToken);
    }

    // Called from inside the cache factory: a hit never reaches upstream, so the flag costs nothing.
    private async Task<VideoDbPlaylist?> FetchAsync(
        VideoDbContentType type,
        int tmdbId,
        CancellationToken cancellationToken
    )
    {
        // Throws rather than returns null: callers discard nulls, so an outage would read as a
        // title nobody carries and be held for as long as one.
        if (cache.TryGetValue(OutageKey, out _)) throw new PlaybackOutageException(Provider);

        try
        {
            return await client.GetPlaylistAsync(type, tmdbId, cancellationToken);
        }
        catch (Exception exception) when (exception.IsUpstreamFault(cancellationToken))
        {
            var window = ITmdbPlaybackProvider.OutageFor(exception);

            cache.Set(OutageKey, true, window);

            logger.ProviderOutageOpened(Provider, window, exception);

            throw;
        }
    }

    private PlaybackSource? BuildSource(string host, VideoDbEntry entry)
    {
        var streams = VideoDbPlaylistParser.ParseStreams(entry.File);
        if (streams.Count is 0) return null;

        // Only an embed origin is accepted as Referer; a ge.movie one is rejected outright.
        var headers = new Dictionary<string, string>
        {
            ["Referer"] = $"https://{host}/",
            ["User-Agent"] = BrowserUserAgent.Value
        };

        return new PlaybackSource(
            Provider,
            headers,
            streams,
            VideoDbPlaylistParser.ParseSubtitles(entry.Subtitles));
    }

    private static IEnumerable<EpisodeNode> EnumerateEpisodes(IReadOnlyList<VideoDbEntry> seasons)
    {
        for (var seasonIndex = 0; seasonIndex < seasons.Count; seasonIndex++)
        {
            var season = seasons[seasonIndex];
            var episodes = season.Folder ?? [];
            var seasonNumber = TrailingNumber(season.Title) ?? seasonIndex + 1;

            for (var episodeIndex = 0; episodeIndex < episodes.Count; episodeIndex++)
            {
                var episode = episodes[episodeIndex];

                var episodeNumber = EpisodeNumberFromId(episode.Id)
                                    ?? TrailingNumber(episode.Title)
                                    ?? episodeIndex + 1;

                yield return new EpisodeNode(seasonNumber, episodeNumber, episode);
            }
        }
    }

    // Episode ids are "{season}-{episode}", which survives translation of the Georgian titles.
    private static int? EpisodeNumberFromId(string? id)
    {
        if (id is null) return null;

        var separator = id.LastIndexOf('-');

        return separator >= 0 && int.TryParse(id.AsSpan(separator + 1), out var episode)
            ? episode
            : null;
    }

    private static int? TrailingNumber(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;

        var match = TrailingNumberRegex().Match(title);

        return match.Success ? int.Parse(match.Groups[1].ValueSpan) : null;
    }

    [GeneratedRegex(@"(\d+)\s*$")]
    private static partial Regex TrailingNumberRegex();

    private sealed record EpisodeNode(int Season, int Episode, VideoDbEntry Entry);
}