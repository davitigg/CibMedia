using CibMedia.Core.Catalog;
using CibMedia.Core.Playback;
using Xunit;

namespace CibMedia.Core.Tests.Playback;

public sealed class WatchProgressTests
{
    [Fact]
    public void Backs_up_so_a_resume_lands_before_the_moment_it_stopped()
    {
        Assert.Equal(590_000, Episode(600_000, season: 1, episode: 2).ResumeFromMs);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4_000)]
    [InlineData(10_000)]
    public void Never_asks_for_a_position_before_the_start(long positionMs)
    {
        Assert.Equal(0, Episode(positionMs, season: 1, episode: 2).ResumeFromMs);
    }

    [Fact]
    public void Resumes_the_episode_the_position_was_stored_against()
    {
        var progress = Episode(600_000, season: 2, episode: 4);

        Assert.Equal(590_000, progress.ResumeMsFor(Target(season: 2, episode: 4)));
    }

    [Theory]
    [InlineData(2, 5)]
    [InlineData(3, 4)]
    [InlineData(1, 1)]
    public void Starts_a_different_episode_from_the_beginning(int season, int episode)
    {
        var progress = Episode(600_000, season: 2, episode: 4);

        Assert.Equal(0, progress.ResumeMsFor(Target(season, episode)));
    }

    [Fact]
    public void Resumes_a_movie_on_its_own_id()
    {
        var progress = Movie(600_000, tmdbId: 550);

        Assert.Equal(590_000, progress.ResumeMsFor(new TitleTarget(MediaId.Movie(550))));
        Assert.Equal(0, progress.ResumeMsFor(new TitleTarget(MediaId.Movie(551))));
    }

    private static TitleTarget Target(int season, int episode)
    {
        return new TitleTarget(MediaId.TvShow(1), season, episode);
    }

    private static WatchProgress Episode(long positionMs, int season, int episode)
    {
        return new WatchProgress(
            MediaId.TvShow(1), "Show", null, null, null, null,
            season, episode, positionMs, 3_600_000, DateTimeOffset.UtcNow);
    }

    private static WatchProgress Movie(long positionMs, int tmdbId)
    {
        return new WatchProgress(
            MediaId.Movie(tmdbId), "Film", null, null, null, null,
            null, null, positionMs, 3_600_000, DateTimeOffset.UtcNow);
    }
}
