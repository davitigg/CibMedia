using CibMedia.Playback.Models;

namespace CibMedia.Playback.Providers;

// A self-contained stack: what it returns is timed against one encode, it answers only for its
// own catalogue, and a title it does not carry is null rather than an error.
internal interface ITmdbPlaybackProvider
{
    // How long a resolved lookup stays good. Shared rather than per-provider: a response is an
    // aggregate and cannot outlive its shortest-lived contributor. The cache is in process and
    // goes with the app, so this is the length of a sitting rather than of a day.
    static readonly TimeSpan SourceTtl = TimeSpan.FromMinutes(15);

    // A title the stack does not carry, which is what stops a rail probing the same absences over
    // and over. Absence is stable in a way an outage is not, so the two do not share a window.
    static readonly TimeSpan AbsentTtl = TimeSpan.FromMinutes(10);

    // What a negative answer is worth: a dead upstream costs one slow request per window rather
    // than one per lookup. A timeout is one slow request and a refusal is the origin asking to be
    // left alone, so they are not worth the same window.
    static readonly TimeSpan TimedOutTtl = TimeSpan.FromSeconds(45);

    static readonly TimeSpan RefusedTtl = TimeSpan.FromMinutes(5);

    static TimeSpan OutageFor(Exception fault)
    {
        return fault is HttpRequestException ? RefusedTtl : TimedOutTtl;
    }

    PlaybackProvider Provider { get; }

    // Every stack is registered whether or not this build turned it on, so the graph keeps one
    // shape and nothing depending on a stack has to know. The flag is read where the stack is used.
    bool Enabled { get; }

    Task<PlaybackSource?> GetMovieAsync(int tmdbId, CancellationToken cancellationToken);

    Task<PlaybackSource?> GetEpisodeAsync(int tmdbId, int season, int episode, CancellationToken cancellationToken);
}
