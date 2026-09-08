using CibMedia.Core.Catalog;
using CibMedia.Core.Common;
using CibMedia.Core.Playback;
using CibMedia.Core.Presentation.Home;
using CibMedia.Core.Presentation.Rails;
using CibMedia.Core.Tests.Fakes;
using Xunit;

namespace CibMedia.Core.Tests.Presentation;

public sealed class HomeViewModelTests
{
    [Fact]
    public void Starts_with_every_rail_loading()
    {
        using var vm = new HomeViewModel(new FakeCatalog(), new FakePlaybackHistory());

        // Rail by rail: RailsState holds a dictionary, which a record compares by reference.
        Assert.All(
            Enum.GetValues<HomeRail>(),
            rail => Assert.IsType<Load<IReadOnlyList<MediaCard>>.Loading>(vm.State.For(rail)));
    }

    [Fact]
    public async Task Fills_every_rail_from_the_catalog()
    {
        using var vm = new HomeViewModel(new FakeCatalog(), new FakePlaybackHistory());

        await vm.LoadAsync();

        Assert.Equal(20, Ready(vm.State, HomeRail.Trending).Count);
        Assert.Equal(20, Ready(vm.State, HomeRail.PopularMovies).Count);
        Assert.Equal(20, Ready(vm.State, HomeRail.PopularTvShows).Count);
        Assert.Equal(20, Ready(vm.State, HomeRail.TopRatedMovies).Count);
        Assert.Equal(20, Ready(vm.State, HomeRail.TopRatedTvShows).Count);
    }

    [Fact]
    public async Task Reports_a_failing_rail_without_taking_down_the_others()
    {
        var catalog = new FakeCatalog
        {
            Trending = (_, _) => Task.FromException<Page<MediaCard>>(new HttpRequestException("no network"))
        };

        using var vm = new HomeViewModel(catalog, new FakePlaybackHistory());

        await vm.LoadAsync();

        var failed = Assert.IsType<Load<IReadOnlyList<MediaCard>>.Failed>(vm.State.For(HomeRail.Trending));
        Assert.Equal(AppErrorKind.Offline, failed.Error.Kind);
        Assert.Equal(20, Ready(vm.State, HomeRail.PopularMovies).Count);
        Assert.Equal(20, Ready(vm.State, HomeRail.TopRatedMovies).Count);
    }

    [Fact]
    public async Task Loads_once_however_many_times_the_page_is_reattached()
    {
        var calls = 0;
        var catalog = new FakeCatalog
        {
            Trending = (_, _) =>
            {
                Interlocked.Increment(ref calls);
                return Task.FromResult(Page<MediaCard>.Empty);
            }
        };

        using var vm = new HomeViewModel(catalog, new FakePlaybackHistory());

        await vm.EnsureLoadedAsync();
        await vm.EnsureLoadedAsync();
        await vm.EnsureLoadedAsync();

        Assert.Equal(1, calls);
    }

    // The load-once gate must not swallow the one rail that is re-read on every arrival.
    [Fact]
    public async Task Continue_watching_still_refreshes_after_the_page_has_loaded()
    {
        var history = new FakePlaybackHistory();

        using var vm = new HomeViewModel(new FakeCatalog(), history);

        await vm.EnsureLoadedAsync();

        Assert.Empty(Ready(vm.State, HomeRail.ContinueWatching));

        await history.RecordAsync(
            new WatchProgress(
                MediaId.Movie(7),
                "Something Watched",
                null,
                null,
                2026,
                7.5,
                null,
                null,
                60_000,
                600_000,
                DateTimeOffset.UtcNow),
            CancellationToken.None);

        await vm.ReloadContinueWatchingAsync();

        Assert.Single(Ready(vm.State, HomeRail.ContinueWatching));
    }

    private static IReadOnlyList<MediaCard> Ready(RailsState<HomeRail> state, HomeRail rail)
    {
        return Assert.IsType<Load<IReadOnlyList<MediaCard>>.Ready>(state.For(rail)).Value;
    }
}
