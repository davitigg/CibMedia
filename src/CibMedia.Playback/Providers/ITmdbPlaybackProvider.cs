using System.Net;
using CibMedia.Playback.Models;

namespace CibMedia.Playback.Providers;

// A self-contained stack: what it returns is timed against one encode, it answers only for its
// own catalogue, and a title it does not carry is null rather than an error.
internal interface ITmdbPlaybackProvider
{
    // How long a lookup stays good, carried or not. Shared rather than per-provider: a response is
    // an aggregate and cannot outlive its shortest-lived contributor. Ten minutes covers stepping in
    // and out of a title's details and then playing it, which is the whole job, and asks nothing of
    // how long an upstream keeps a signed url alive. The cache goes with the app either way.
    static readonly TimeSpan AnswerTtl = TimeSpan.FromMinutes(10);

    // What a negative answer is worth: a dead upstream costs one slow request per window rather
    // than one per lookup. Both are short because a window re-arms — when it lapses one request
    // goes out, and a stack still down opens it again — so length buys nothing but fewer probes,
    // while a window opened in error skips every title for the whole of it.
    static readonly TimeSpan TimedOutTtl = TimeSpan.FromSeconds(20);

    // A minute is the span a rate limiter is usually counting over, and one probe a minute is not
    // the burst it is counting.
    static readonly TimeSpan RefusedTtl = TimeSpan.FromMinutes(1);

    // Only an origin turning this box away earns the long window: that is the one fault retrying
    // makes worse. Everything else — a 502, a request that ran out of time — is the upstream having
    // a moment, which the short window is long enough for.
    static TimeSpan OutageFor(Exception fault)
    {
        return fault is HttpRequestException
        {
            StatusCode: HttpStatusCode.TooManyRequests or HttpStatusCode.Forbidden
        }
            ? RefusedTtl
            : TimedOutTtl;
    }

    PlaybackProvider Provider { get; }

    // Every stack is registered whether or not this build turned it on, so the graph keeps one
    // shape and nothing depending on a stack has to know. The flag is read where the stack is used.
    bool Enabled { get; }

    Task<PlaybackSource?> GetMovieAsync(int tmdbId, CancellationToken cancellationToken);

    Task<PlaybackSource?> GetEpisodeAsync(int tmdbId, int season, int episode, CancellationToken cancellationToken);
}
