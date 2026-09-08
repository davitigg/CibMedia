using System.Text.Json.Serialization;

namespace CibMedia.Playback.Models;

// One source per provider that answered, best first. IsPartial is never cached with the response:
// an aggregate short of a provider must not be held for as long as a whole one.
public sealed record ResolvedPlayback(
    PlaybackMediaType MediaType,
    IReadOnlyList<PlaybackSource> Sources,
    [property: JsonIgnore] bool IsPartial = false);
