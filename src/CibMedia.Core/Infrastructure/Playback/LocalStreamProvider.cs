using System.Net;
using CibMedia.Core.Abstractions;
using CibMedia.Core.Common;
using CibMedia.Core.Playback;
using CibMedia.Playback.Services;

namespace CibMedia.Core.Infrastructure.Playback;

public sealed class LocalStreamProvider(TmdbPlaybackService titles, LiveballPlaybackService liveball)
    : IStreamProvider
{
    public async Task<PlayableStream> ResolveAsync(PlaybackTarget target, CancellationToken ct)
    {
        try
        {
            var resolved = target switch
            {
                TitleTarget { SeasonNumber: { } season, EpisodeNumber: { } episode } show =>
                    await titles.GetEpisodeAsync(show.Id.TmdbId, season, episode, ct).ConfigureAwait(false),
                TitleTarget movie =>
                    await titles.GetMovieAsync(movie.Id.TmdbId, ct).ConfigureAwait(false),
                LiveballTarget page =>
                    await liveball.GetAsync(page.Url, ct).ConfigureAwait(false),
                _ => throw new ArgumentOutOfRangeException(nameof(target))
            };

            return resolved is null ? throw PlaybackMapping.NoStream() : resolved.ToPlayableStream();
        }
        catch (HttpRequestException exception)
            when (exception.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            // The upstream refused this box, which is not the box failing to reach it.
            throw new UpstreamException(AppErrorKind.Rejected, exception.Message);
        }
        catch (AggregateException exception)
        {
            // Every stack fell over, which says nothing about whether the title exists.
            throw new UpstreamException(AppErrorKind.Server, exception.Message);
        }
    }
}
