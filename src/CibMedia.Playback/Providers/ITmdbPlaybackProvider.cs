using CibMedia.Playback.Models;

namespace CibMedia.Playback.Providers;

// A self-contained stack: what it returns is timed against one encode, it answers only for its
// own catalogue, and a title it does not carry is null rather than an error.
internal interface ITmdbPlaybackProvider
{
    // How long a resolved lookup stays good. Shared rather than per-provider: a response is an
    // aggregate and cannot outlive its shortest-lived contributor.
    static readonly TimeSpan SourceTtl = TimeSpan.FromHours(2);

    // What a negative answer is worth: a dead upstream costs one slow request per window rather
    // than one per lookup.
    static readonly TimeSpan OutageTtl = TimeSpan.FromMinutes(10);

    PlaybackProvider Provider { get; }

    // Every stack is registered whether or not this build turned it on, so the graph keeps one
    // shape and nothing depending on a stack has to know. The flag is read where the stack is used.
    bool Enabled { get; }

    Task<PlaybackSource?> GetMovieAsync(int tmdbId, CancellationToken cancellationToken);

    Task<PlaybackSource?> GetEpisodeAsync(int tmdbId, int season, int episode, CancellationToken cancellationToken);
}
