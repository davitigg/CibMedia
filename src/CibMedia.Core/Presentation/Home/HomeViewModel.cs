using CibMedia.Core.Abstractions;
using CibMedia.Core.Catalog;
using CibMedia.Core.Common;
using CibMedia.Core.Presentation.Rails;

namespace CibMedia.Core.Presentation.Home;

public sealed class HomeViewModel : RailsViewModel<HomeRail>
{
    private readonly IPlaybackHistory _history;

    public HomeViewModel(ICatalog catalog, IPlaybackHistory history)
        : base(Rails(catalog, history))
    {
        _history = history;
        _history.Changed += OnHistoryChanged;
    }

    // Also called on arrival at the page, for a write that landed while it did not exist.
    public Task ReloadContinueWatchingAsync()
    {
        return ReloadAsync(HomeRail.ContinueWatching);
    }

    protected override void OnDispose()
    {
        _history.Changed -= OnHistoryChanged;
    }

    private void OnHistoryChanged()
    {
        _ = ReloadContinueWatchingAsync();
    }

    private static Rail<HomeRail>[] Rails(ICatalog catalog, IPlaybackHistory history)
    {
        return
        [
            new(HomeRail.ContinueWatching, (_, ct) => ContinueWatchingAsync(history, ct)),
            new(HomeRail.Trending, catalog.GetTrendingAsync),
            new(HomeRail.PopularMovies, catalog.GetPopularMoviesAsync),
            new(HomeRail.PopularTvShows, catalog.GetPopularTvShowsAsync),
            new(HomeRail.TopRatedMovies, catalog.GetTopRatedMoviesAsync),
            new(HomeRail.TopRatedTvShows, catalog.GetTopRatedTvShowsAsync)
        ];
    }

    // Watch history is local and capped, so it arrives whole: one page, never a second.
    private static async Task<Page<MediaCard>> ContinueWatchingAsync(
        IPlaybackHistory history,
        CancellationToken ct)
    {
        var recent = await history.RecentAsync(ct).ConfigureAwait(false);

        return new Page<MediaCard>(
            [
                .. recent.Select(p => new MediaCard(
                    p.Id, p.Title, p.PosterUrl, p.Year, p.Rating, p.BackdropUrl, p.SeasonNumber, p.EpisodeNumber))
            ],
            1,
            1);
    }
}
