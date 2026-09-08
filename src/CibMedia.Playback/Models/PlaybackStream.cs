using System.Text.Json.Serialization;

namespace CibMedia.Playback.Models;

// Label is what a picker shows: an xpass server, a liveball channel, a videodb audio language and
// quality. Never empty, and unique within a source.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "protocol")]
[JsonDerivedType(typeof(HlsStream), "hls")]
[JsonDerivedType(typeof(ProgressiveStream), "progressive")]
public abstract record PlaybackStream(string Url, string Label);
