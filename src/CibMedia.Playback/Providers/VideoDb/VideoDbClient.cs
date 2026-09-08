using CibMedia.Playback.Logging;
using System.Net;
using System.Text.Json;
using CibMedia.Playback.Providers.VideoDb.Models;

namespace CibMedia.Playback.Providers.VideoDb;

internal sealed class VideoDbClient(HttpClient http, VideoDbOptions options, ILogger<VideoDbClient> logger)
{
    // The whole catalogue entry, or null when the title is not in it. Serials ignore season and
    // episode filtering upstream, so the complete season tree always comes back. Throws
    // HttpRequestException only once every mirror has failed to answer.
    public async Task<VideoDbPlaylist?> GetPlaylistAsync(
        VideoDbContentType type,
        int tmdbId,
        CancellationToken cancellationToken
    )
    {
        var name = type is VideoDbContentType.Movie ? "movie" : "serial";

        // Mirrors are raced rather than tried in order: they front one backend, so the first answer
        // is as good as any, and a request now costs one timeout instead of one per host. Serverless
        // compute bills the whole wait, so three dead mirrors in sequence held a billed slot for 3x
        // the timeout.
        using var attempts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var pending = options.Hosts
            .Select(host => TryGetAsync(host, name, tmdbId, attempts.Token))
            .ToList();

        var failures = new List<Exception>();
        var missed = false;

        await foreach (var completed in Task.WhenEach(pending).WithCancellation(cancellationToken))
        {
            var attempt = await completed;

            if (attempt.Playlist is { } playlist)
            {
                await attempts.CancelAsync();

                return playlist;
            }

            if (attempt.Error is { } error) failures.Add(error);
            else missed = true;
        }

        // A mirror answering "not in catalogue" is authoritative even when its siblings fell over.
        if (missed) return null;

        throw new HttpRequestException(
            $"No videodb mirror answered for {name} {tmdbId} ({string.Join(", ", options.Hosts)}).",
            failures.FirstOrDefault());
    }

    private async Task<Attempt> TryGetAsync(
        string host,
        string name,
        int tmdbId,
        CancellationToken cancellationToken
    )
    {
        var url = $"https://{host}/file/play?type={name}&id={tmdbId}&name={name}&lang=ka&p=l.playlist";

        try
        {
            using var response = await http.GetAsync(url, cancellationToken);

            if (response.StatusCode is HttpStatusCode.NotFound) return Attempt.Miss;

            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var entries = Deserialize(body);

            // A 200 that isn't a populated array is the catalogue saying no — upstream answers
            // misses with a plain-text notice as often as with a 404.
            return entries is null or []
                ? Attempt.Miss
                : new Attempt(new VideoDbPlaylist(host, entries), null);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            // Losing the race cancels the token, which is not worth logging as a mirror failure.
            if (!cancellationToken.IsCancellationRequested)
                logger.VideoDbMirrorFailed(host, $"{name} {tmdbId}", exception);

            return new Attempt(null, exception);
        }
    }

    private static List<VideoDbEntry>? Deserialize(string body)
    {
        try
        {
            return JsonSerializer.Deserialize<List<VideoDbEntry>>(body, JsonSerializerOptions.Web);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record Attempt(VideoDbPlaylist? Playlist, Exception? Error)
    {
        public static readonly Attempt Miss = new(null, null);
    }
}