using CibMedia.Playback.Models;

namespace CibMedia.Playback.Providers.Xpass.Models;

// Cache shape. Carries the subtitles too, so a cache hit reaches no upstream at all.
internal sealed record XpassResolved(IReadOnlyList<PlaybackStream> Streams, IReadOnlyList<PlaybackSubtitle> Subtitles);