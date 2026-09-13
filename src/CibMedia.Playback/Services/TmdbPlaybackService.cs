using CibMedia.Playback.Logging;
using System.Diagnostics;
using CibMedia.Playback.Models;
using CibMedia.Playback.Providers;

namespace CibMedia.Playback.Services;

public sealed class TmdbPlaybackService
{
    // What a caller waits, whatever the stacks are doing. Each stack bounds its own hops and this
    // is the only thing that bounds their sum: a cold bundle walk in front of a full probe reaches
    // half a minute, and the spinner is on the details page for the whole of it.
    //
    // A cut here is this side's, so it is not evidence about a stack and opens no outage window,
    // and the run it cut is left to finish into the cache — the next press reads the answer it
    // produced rather than starting the work again.
    private static readonly TimeSpan LookupBudget = TimeSpan.FromSeconds(20);

    private readonly List<ITmdbPlaybackProvider> _enabled;
    private readonly ILogger<TmdbPlaybackService> _logger;

    internal TmdbPlaybackService(IEnumerable<ITmdbPlaybackProvider> providers, ILogger<TmdbPlaybackService> logger)
    {
        _enabled = providers.Where(provider => provider.Enabled).ToList();
        _logger = logger;
    }

    public Task<ResolvedPlayback?> GetMovieAsync(int tmdbId, CancellationToken cancellationToken)
    {
        return ResolveAsync(
            $"Movie {tmdbId}",
            PlaybackMediaType.Movie,
            (provider, token) => provider.GetMovieAsync(tmdbId, token),
            cancellationToken);
    }

    public Task<ResolvedPlayback?> GetEpisodeAsync(
        int tmdbId,
        int season,
        int episode,
        CancellationToken cancellationToken
    )
    {
        return ResolveAsync(
            $"TV Show {tmdbId} S{season}E{episode}",
            PlaybackMediaType.TvShow,
            (provider, token) => provider.GetEpisodeAsync(tmdbId, season, episode, token),
            cancellationToken);
    }

    // A provider that fell over accounts for itself, so the outcome stays at information whether or
    // not the answer is whole.
    private async Task<ResolvedPlayback?> ResolveAsync(
        string lookup,
        PlaybackMediaType mediaType,
        Func<ITmdbPlaybackProvider, CancellationToken, Task<PlaybackSource?>> resolve,
        CancellationToken cancellationToken
    )
    {
        var started = Stopwatch.GetTimestamp();

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(LookupBudget);

        var sources = await GatherAsync(lookup, resolve, budget.Token, cancellationToken);

        var elapsed = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;

        if (sources.Count is 0)
        {
            _logger.PlaybackMissing(lookup, elapsed);

            return null;
        }

        _logger.PlaybackResolved(lookup, sources.Count, _enabled.Count, Tally(sources), elapsed);

        return new ResolvedPlayback(mediaType, sources);
    }

    // What each stack carried, not just that it answered: a stack down to one stream where it
    // usually offers several is a probe that ran out of time, and nothing else records that.
    private static string Tally(List<PlaybackSource> sources)
    {
        return string.Join(", ", sources.Select(source => $"{source.Provider} {source.Streams.Count}"));
    }

    // One stack being down thins the response rather than failing it, which is why the tally above
    // is logged: it is the only record that an answer came back short.
    private async Task<List<PlaybackSource>> GatherAsync(
        string lookup,
        Func<ITmdbPlaybackProvider, CancellationToken, Task<PlaybackSource?>> resolve,
        CancellationToken budget,
        CancellationToken cancellationToken
    )
    {
        var outcomes = await Task.WhenAll(
            _enabled.Select(provider => RunAsync(provider, lookup, resolve, budget, cancellationToken)));

        var failures = outcomes.Select(outcome => outcome.Error).OfType<Exception>().ToList();

        // Nobody answered at all, so an empty list would be a claim rather than an answer.
        if (failures.Count > 0 && failures.Count == outcomes.Length)
            throw new AggregateException($"Every playback provider failed for {lookup}.", failures);

        return outcomes.Select(outcome => outcome.Source).OfType<PlaybackSource>().ToList();
    }

    private async Task<Outcome> RunAsync(
        ITmdbPlaybackProvider provider,
        string lookup,
        Func<ITmdbPlaybackProvider, CancellationToken, Task<PlaybackSource?>> resolve,
        CancellationToken budget,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return new Outcome(await resolve(provider, budget), null);
        }
        catch (PlaybackOutageException exception)
        {
            _logger.ProviderSkipped(provider.Provider, lookup);

            return new Outcome(null, exception);
        }
        // The caller walked off the screen. Not a fault, and not an answer either: reported as one
        // it would be a warning per stack for every title anyone stepped out of.
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        // The budget, and only the budget: an upstream's own timeout arrives as a cancellation too,
        // and that one is evidence about the stack rather than about how long this lookup ran.
        catch (OperationCanceledException exception) when (budget.IsCancellationRequested)
        {
            _logger.ProviderUnfinished(provider.Provider, lookup, LookupBudget);

            return new Outcome(null, exception);
        }
        catch (Exception exception)
        {
            _logger.ProviderFailed(provider.Provider, lookup, exception);

            return new Outcome(null, exception);
        }
    }

    private sealed record Outcome(PlaybackSource? Source, Exception? Error);
}
