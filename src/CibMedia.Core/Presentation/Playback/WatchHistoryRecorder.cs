using CibMedia.Core.Abstractions;
using CibMedia.Core.Playback;
using CibMedia.Core.Common;

namespace CibMedia.Core.Presentation.Playback;

// A finished episode is rolled forward to the next one rather than recorded as watched
// through, so the series stays on Continue Watching offering what comes next. A movie, or
// the last episode of a series, has nothing to roll forward to and drops off the rail.
public sealed class WatchHistoryRecorder(ICatalog catalog, IPlaybackHistory history)
{
    public async Task RecordAsync(WatchProgress progress, CancellationToken ct)
    {
        if (progress is { IsFinished: true, SeasonNumber: { } season, EpisodeNumber: { } episode }
            && await NextEpisodeAsync(progress.Id.TmdbId, season, episode, ct).ConfigureAwait(false) is { } next)
            progress = progress with
            {
                SeasonNumber = next.Season,
                EpisodeNumber = next.Episode,
                PositionMs = 0,
                DurationMs = 0
            };

        await history.RecordAsync(progress, ct).ConfigureAwait(false);
    }

    private async Task<(int Season, int Episode)?> NextEpisodeAsync(
        int tmdbId,
        int season,
        int episode,
        CancellationToken ct)
    {
        var episodes = (await catalog.GetSeasonAsync(tmdbId, season, ct).ConfigureAwait(false)).Episodes;
        var index = episodes.FindIndex(e => e.EpisodeNumber == episode);

        if (index >= 0 && index + 1 < episodes.Count) return (season, episodes[index + 1].EpisodeNumber);

        var seasons = (await catalog.GetTvShowAsync(tmdbId, ct).ConfigureAwait(false)).Seasons
            .OrderBy(s => s.SeasonNumber)
            .ToList();

        var seasonIndex = seasons.FindIndex(s => s.SeasonNumber == season);

        return seasonIndex >= 0 && seasonIndex + 1 < seasons.Count
            ? (seasons[seasonIndex + 1].SeasonNumber, 1)
            : null;
    }
}
