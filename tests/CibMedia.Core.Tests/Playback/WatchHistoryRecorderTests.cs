using CibMedia.Core.Catalog;
using CibMedia.Core.Playback;
using CibMedia.Core.Presentation.Playback;
using CibMedia.Core.Tests.Fakes;
using Xunit;

namespace CibMedia.Core.Tests.Playback;

public sealed class WatchHistoryRecorderTests
{
    [Fact]
    public async Task Rolls_a_finished_episode_forward_to_the_next_one()
    {
        var history = new FakePlaybackHistory();
        var recorder = new WatchHistoryRecorder(new FakeCatalog(), history);

        await recorder.RecordAsync(Finished(season: 2, episode: 3), TestContext.Current.CancellationToken);

        var stored = await history.ForAsync(MediaId.TvShow(42), TestContext.Current.CancellationToken);

        Assert.Equal(2, stored?.SeasonNumber);
        Assert.Equal(4, stored?.EpisodeNumber);
        Assert.Equal(0, stored?.PositionMs);
    }

    [Fact]
    public async Task Rolls_the_last_episode_of_a_season_into_the_next_season()
    {
        var history = new FakePlaybackHistory();
        var recorder = new WatchHistoryRecorder(new FakeCatalog(), history);

        await recorder.RecordAsync(Finished(season: 2, episode: 10), TestContext.Current.CancellationToken);

        var stored = await history.ForAsync(MediaId.TvShow(42), TestContext.Current.CancellationToken);

        Assert.Equal(3, stored?.SeasonNumber);
        Assert.Equal(1, stored?.EpisodeNumber);
    }

    [Fact]
    public async Task Records_the_last_episode_of_a_series_as_it_stands()
    {
        var history = new FakePlaybackHistory();
        var recorder = new WatchHistoryRecorder(new FakeCatalog(), history);

        await recorder.RecordAsync(Finished(season: 4, episode: 10), TestContext.Current.CancellationToken);

        var stored = await history.ForAsync(MediaId.TvShow(42), TestContext.Current.CancellationToken);

        Assert.Equal(4, stored?.SeasonNumber);
        Assert.Equal(10, stored?.EpisodeNumber);
        Assert.True(stored?.IsFinished);
    }

    [Fact]
    public async Task Leaves_a_part_watched_episode_where_it_is()
    {
        var history = new FakePlaybackHistory();
        var recorder = new WatchHistoryRecorder(new FakeCatalog(), history);

        var progress = Finished(season: 2, episode: 3) with { PositionMs = 60_000 };

        await recorder.RecordAsync(progress, TestContext.Current.CancellationToken);

        var stored = await history.ForAsync(MediaId.TvShow(42), TestContext.Current.CancellationToken);

        Assert.Equal(3, stored?.EpisodeNumber);
        Assert.Equal(60_000, stored?.PositionMs);
    }

    [Fact]
    public async Task Records_a_finished_movie_without_asking_the_catalog()
    {
        var history = new FakePlaybackHistory();
        var recorder = new WatchHistoryRecorder(new FakeCatalog(), history);

        var movie = Finished(season: 2, episode: 3) with
        {
            Id = MediaId.Movie(42),
            SeasonNumber = null,
            EpisodeNumber = null
        };

        await recorder.RecordAsync(movie, TestContext.Current.CancellationToken);

        var stored = await history.ForAsync(MediaId.Movie(42), TestContext.Current.CancellationToken);

        Assert.True(stored?.IsFinished);
        Assert.Null(stored?.SeasonNumber);
    }

    private static WatchProgress Finished(int season, int episode)
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
            600_000,
            600_000,
            DateTimeOffset.UtcNow);
    }
}
