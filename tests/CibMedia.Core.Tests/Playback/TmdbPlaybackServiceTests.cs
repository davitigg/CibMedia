using CibMedia.Playback.Models;
using CibMedia.Playback.Providers;
using CibMedia.Playback.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CibMedia.Core.Tests.Playback;

public sealed class TmdbPlaybackServiceTests
{
    [Fact]
    public async Task Answers_with_the_stacks_that_carried_it()
    {
        var service = Service(
            FakeProvider.Carrying(PlaybackProvider.Xpass),
            FakeProvider.Failing(PlaybackProvider.VideoDb));

        var resolved = await service.GetMovieAsync(1, TestContext.Current.CancellationToken);

        Assert.Equal([PlaybackProvider.Xpass], resolved!.Sources.Select(source => source.Provider));
    }

    // An empty list would read as a title nobody carries, which is a claim nothing here can make.
    [Fact]
    public async Task Fails_when_every_stack_failed()
    {
        var service = Service(
            FakeProvider.Failing(PlaybackProvider.Xpass),
            FakeProvider.Failing(PlaybackProvider.VideoDb));

        await Assert.ThrowsAsync<AggregateException>(async () =>
            await service.GetMovieAsync(1, TestContext.Current.CancellationToken));
    }

    // Stepping out of a title cancels its lookup, and a cancellation is not a fault: turned into
    // one it reaches the screen as an error and the log as a warning per stack.
    [Fact]
    public async Task Cancels_rather_than_fails_when_the_caller_walked_away()
    {
        using var left = new CancellationTokenSource();
        await left.CancelAsync();

        var service = Service(
            FakeProvider.Carrying(PlaybackProvider.Xpass),
            FakeProvider.Carrying(PlaybackProvider.VideoDb));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await service.GetMovieAsync(1, left.Token));
    }

    // A stack this build turned off is registered like any other and must not count as one that
    // failed, or every lookup would fail as soon as one was switched off.
    [Fact]
    public async Task Leaves_a_disabled_stack_out_of_the_lookup()
    {
        var service = Service(
            FakeProvider.Carrying(PlaybackProvider.Xpass),
            FakeProvider.Disabled(PlaybackProvider.VideoDb));

        var resolved = await service.GetMovieAsync(1, TestContext.Current.CancellationToken);

        Assert.Equal([PlaybackProvider.Xpass], resolved!.Sources.Select(source => source.Provider));
    }

    private static TmdbPlaybackService Service(params ITmdbPlaybackProvider[] providers)
    {
        return new TmdbPlaybackService(providers, NullLogger<TmdbPlaybackService>.Instance);
    }

    private sealed class FakeProvider : ITmdbPlaybackProvider
    {
        private readonly bool _carries;

        private FakeProvider(PlaybackProvider provider, bool enabled, bool carries)
        {
            Provider = provider;
            Enabled = enabled;
            _carries = carries;
        }

        public PlaybackProvider Provider { get; }

        public bool Enabled { get; }

        public static FakeProvider Carrying(PlaybackProvider provider) => new(provider, true, true);

        public static FakeProvider Failing(PlaybackProvider provider) => new(provider, true, false);

        public static FakeProvider Disabled(PlaybackProvider provider) => new(provider, false, true);

        public Task<PlaybackSource?> GetMovieAsync(int tmdbId, CancellationToken cancellationToken)
        {
            return AnswerAsync(cancellationToken);
        }

        public Task<PlaybackSource?> GetEpisodeAsync(
            int tmdbId,
            int season,
            int episode,
            CancellationToken cancellationToken
        )
        {
            return AnswerAsync(cancellationToken);
        }

        private Task<PlaybackSource?> AnswerAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_carries) throw new HttpRequestException("upstream");

            return Task.FromResult<PlaybackSource?>(new PlaybackSource(
                Provider,
                new Dictionary<string, string>(),
                [new HlsStream("https://cdn.example/master.m3u8", "Stream")],
                []));
        }
    }
}
