using CibMedia.Playback.Logging;
using System.Diagnostics;
using CibMedia.Playback.Models;
using CibMedia.Playback.Providers;

namespace CibMedia.Playback.Services;

public sealed class TmdbPlaybackService
{
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
            provider => provider.GetMovieAsync(tmdbId, cancellationToken));
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
            provider => provider.GetEpisodeAsync(tmdbId, season, episode, cancellationToken));
    }

    // A provider that fell over accounts for itself, so the outcome stays at information whether or
    // not the answer is whole.
    private async Task<ResolvedPlayback?> ResolveAsync(
        string lookup,
        PlaybackMediaType mediaType,
        Func<ITmdbPlaybackProvider, Task<PlaybackSource?>> resolve
    )
    {
        var started = Stopwatch.GetTimestamp();

        var (sources, degraded) = await GatherAsync(lookup, resolve);

        var elapsed = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;

        if (sources.Count is 0)
        {
            _logger.PlaybackMissing(lookup, elapsed);

            return null;
        }

        _logger.PlaybackResolved(lookup, sources.Count, _enabled.Count, elapsed);

        return new ResolvedPlayback(mediaType, sources, degraded);
    }

    // One stack being down degrades the response instead of failing it, and saying so is what
    // keeps that thinner answer from being cached as long as a whole one.
    private async Task<(List<PlaybackSource> Sources, bool Degraded)> GatherAsync(
        string lookup,
        Func<ITmdbPlaybackProvider, Task<PlaybackSource?>> resolve
    )
    {
        var outcomes = await Task.WhenAll(_enabled.Select(provider => RunAsync(provider, lookup, resolve)));

        var failures = outcomes.Select(outcome => outcome.Error).OfType<Exception>().ToList();

        // Nobody answered at all, so an empty list would be a claim rather than an answer.
        if (failures.Count > 0 && failures.Count == outcomes.Length)
            throw new AggregateException($"Every playback provider failed for {lookup}.", failures);

        return (outcomes.Select(outcome => outcome.Source).OfType<PlaybackSource>().ToList(), failures.Count > 0);
    }

    private async Task<Outcome> RunAsync(
        ITmdbPlaybackProvider provider,
        string lookup,
        Func<ITmdbPlaybackProvider, Task<PlaybackSource?>> resolve
    )
    {
        try
        {
            return new Outcome(await resolve(provider), null);
        }
        catch (PlaybackOutageException exception)
        {
            _logger.ProviderSkipped(provider.Provider, lookup);

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