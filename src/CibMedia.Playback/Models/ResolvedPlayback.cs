namespace CibMedia.Playback.Models;

// One source per provider that answered, best first. A stack that fell over contributes nothing
// and caches nothing, so the next lookup asks it again and this carries no record of the gap.
public sealed record ResolvedPlayback(PlaybackMediaType MediaType, IReadOnlyList<PlaybackSource> Sources);
