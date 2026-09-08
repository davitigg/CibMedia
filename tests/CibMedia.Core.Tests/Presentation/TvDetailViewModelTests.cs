using CibMedia.Core.Catalog;
using CibMedia.Core.Common;
using CibMedia.Core.Playback;
using CibMedia.Core.Presentation.TvDetail;
using CibMedia.Core.Tests.Fakes;
using Xunit;

namespace CibMedia.Core.Tests.Presentation;

public sealed class TvDetailViewModelTests
{
    [Fact]
    public async Task Fetches_a_season_once_however_often_it_is_reselected()
    {
        var inner = new FakeCatalog();
        var fetches = 0;

        var catalog = new FakeCatalog
        {
            Season = (tmdbId, seasonNumber, ct) =>
            {
                Interlocked.Increment(ref fetches);
                return inner.GetSeasonAsync(tmdbId, seasonNumber, ct);
            }
        };

        using var vm = new TvDetailViewModel(catalog, new FakePlaybackHistory(), new FakeStreamProvider(), new FakePlaybackPreferences(), 42);

        await vm.LoadAsync();

        var afterFirstSeason = fetches;

        await vm.SelectSeasonAsync(2);
        await vm.SelectSeasonAsync(1);
        await vm.SelectSeasonAsync(2);
        await vm.SelectSeasonAsync(1);

        Assert.Equal(afterFirstSeason + 1, fetches);
        Assert.IsType<Load<SeasonDetails>.Ready>(vm.State.Season);
        Assert.Equal(1, vm.State.SelectedSeason);
    }

    [Fact]
    public async Task Opens_on_the_season_being_watched()
    {
        var history = new FakePlaybackHistory();
        await history.RecordAsync(PartWayThrough(season: 3, episode: 1), CancellationToken.None);

        using var vm = new TvDetailViewModel(new FakeCatalog(), history, new FakeStreamProvider(), new FakePlaybackPreferences(), 42);

        await vm.LoadAsync();

        Assert.Equal(3, vm.State.SelectedSeason);
        Assert.Equal(3, Assert.IsType<Load<SeasonDetails>.Ready>(vm.State.Season).Value.SeasonNumber);
    }

    [Fact]
    public async Task Never_changes_the_selected_season_on_its_own()
    {
        var history = new FakePlaybackHistory();

        using var vm = new TvDetailViewModel(new FakeCatalog(), history, new FakeStreamProvider(), new FakePlaybackPreferences(), 42);

        await vm.LoadAsync();
        await vm.SelectSeasonAsync(4);

        await history.RecordAsync(PartWayThrough(season: 2, episode: 1), CancellationToken.None);
        await vm.ReloadResumeAsync();

        Assert.Equal(4, vm.State.SelectedSeason);
        Assert.Equal(2, vm.State.Resume?.SeasonNumber);
    }

    [Fact]
    public async Task Re_checks_the_providers_when_the_play_target_moves()
    {
        var streams = new FakeStreamProvider();

        using var vm = new TvDetailViewModel(
            new FakeCatalog(), new FakePlaybackHistory(), streams, new FakePlaybackPreferences(), 42);

        await vm.LoadAsync();
        await vm.SelectSeasonAsync(2);

        Assert.Equal(2, vm.State.PlayTarget?.SeasonNumber);
        Assert.Equal(["VideoDb"], Assert.IsType<Load<IReadOnlyList<string>>.Ready>(vm.State.Providers).Value);
        Assert.Equal("VideoDb", vm.State.Provider);
        Assert.Equal([1, 2], streams.Resolved.OfType<TitleTarget>().Select(target => target.SeasonNumber));
    }

    [Fact]
    public async Task Does_not_re_check_while_the_play_target_stands_still()
    {
        var streams = new FakeStreamProvider();

        using var vm = new TvDetailViewModel(
            new FakeCatalog(), new FakePlaybackHistory(), streams, new FakePlaybackPreferences(), 42);

        await vm.LoadAsync();

        var afterLoad = streams.Resolved.Count;

        await vm.ReloadResumeAsync();
        await vm.SelectSeasonAsync(1);
        await vm.ReloadResumeAsync();

        Assert.Equal(afterLoad, streams.Resolved.Count);
    }

    [Fact]
    public async Task Leaves_the_providers_unknown_when_the_check_could_not_be_made()
    {
        var streams = new FakeStreamProvider
        {
            Resolve = (_, _) => Task.FromException<PlayableStream>(new HttpRequestException("dropped"))
        };

        using var vm = new TvDetailViewModel(
            new FakeCatalog(), new FakePlaybackHistory(), streams, new FakePlaybackPreferences(), 42);

        await vm.LoadAsync();

        var failed = Assert.IsType<Load<IReadOnlyList<string>>.Failed>(vm.State.Providers);
        Assert.NotEqual(AppErrorKind.NotFound, failed.Error.Kind);
        Assert.Null(vm.State.Provider);
    }

    [Fact]
    public async Task Forgets_the_providers_while_a_new_target_is_checked()
    {
        var gate = new TaskCompletionSource<PlayableStream>();
        var streams = new FakeStreamProvider();

        using var vm = new TvDetailViewModel(
            new FakeCatalog(), new FakePlaybackHistory(), streams, new FakePlaybackPreferences(), 42);

        await vm.LoadAsync();

        streams.Resolve = (_, _) => gate.Task;

        var sawUnknown = false;
        vm.StateChanged += () => sawUnknown |= vm.State.Providers is Load<IReadOnlyList<string>>.Loading;

        var selecting = vm.SelectSeasonAsync(2);
        gate.SetResult(Offered("Xpass"));
        await selecting;

        Assert.True(sawUnknown);
        Assert.Equal("Xpass", vm.State.Provider);
    }

    [Fact]
    public async Task Reports_no_stream_when_there_is_no_source()
    {
        var streams = new FakeStreamProvider
        {
            Resolve = (_, _) => Task.FromException<PlayableStream>(new UpstreamException(AppErrorKind.NotFound, "none"))
        };

        using var vm = new TvDetailViewModel(
            new FakeCatalog(), new FakePlaybackHistory(), streams, new FakePlaybackPreferences(), 42);

        await vm.LoadAsync();

        var failed = Assert.IsType<Load<IReadOnlyList<string>>.Failed>(vm.State.Providers);
        Assert.Equal(AppErrorKind.NotFound, failed.Error.Kind);
        Assert.Null(vm.State.Provider);
    }

    [Fact]
    public async Task Reselecting_a_loaded_season_never_returns_to_loading()
    {
        using var vm = new TvDetailViewModel(new FakeCatalog(), new FakePlaybackHistory(), new FakeStreamProvider(), new FakePlaybackPreferences(), 42);

        await vm.LoadAsync();
        await vm.SelectSeasonAsync(2);

        var sawLoading = false;
        vm.StateChanged += () =>
        {
            if (vm.State.Season is Load<SeasonDetails>.Loading) sawLoading = true;
        };

        await vm.SelectSeasonAsync(1);
        await vm.SelectSeasonAsync(2);

        Assert.False(sawLoading);
    }

    [Fact]
    public async Task Stepping_the_provider_walks_the_ones_offered_and_stays_put_with_one()
    {
        var two = new FakeStreamProvider { Resolve = (_, _) => Task.FromResult(Offered("VideoDb", "Xpass")) };

        using var vm = new TvDetailViewModel(
            new FakeCatalog(), new FakePlaybackHistory(), two, new FakePlaybackPreferences(), 42);

        await vm.LoadAsync();

        Assert.Equal("VideoDb", vm.State.Provider);

        vm.NextProvider();
        Assert.Equal("Xpass", vm.State.Provider);

        vm.NextProvider();
        Assert.Equal("VideoDb", vm.State.Provider);

        using var lone = new TvDetailViewModel(
            new FakeCatalog(), new FakePlaybackHistory(), new FakeStreamProvider(), new FakePlaybackPreferences(), 42);

        await lone.LoadAsync();

        var before = lone.State;
        lone.NextProvider();

        Assert.Equal("VideoDb", before.Provider);
        Assert.Same(before, lone.State);
    }

    private static PlayableStream Offered(params string[] providers)
    {
        return new PlayableStream(
        [
            .. providers.Select(name => new ProviderStreams(
                name,
                new Dictionary<string, string>(),
                [],
                [new StreamEntry($"https://{name}.test/m.m3u8", true, "Stream", null)]))
        ]);
    }

    private static WatchProgress PartWayThrough(int season, int episode)
    {
        return new WatchProgress(
            MediaId.TvShow(42),
            "Some Series",
            null,
            null,
            2026,
            8.0,
            season,
            episode,
            60_000,
            600_000,
            DateTimeOffset.UtcNow);
    }
}
