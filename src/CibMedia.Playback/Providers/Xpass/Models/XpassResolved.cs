using CibMedia.Playback.Models;

namespace CibMedia.Playback.Providers.Xpass.Models;

// Cache shape. Carries the subtitles too, so a cache hit reaches no upstream at all. ServersOffered
// tells a title the catalogue does not hold from one whose servers all failed to verify, which is
// the difference between an answer worth keeping and one worth asking again shortly.
internal sealed record XpassResolved(
    int ServersOffered,
    IReadOnlyList<PlaybackStream> Streams,
    IReadOnlyList<PlaybackSubtitle> Subtitles);
