using System.Text.Json.Serialization;

namespace CibMedia.Core.Presentation.Remote;

// Source-generated because reflection-based serialisation silently produces empty objects
// under TrimMode=full, in Release only.
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(PlayMovieCommand))]
[JsonSerializable(typeof(PlayTvShowCommand))]
[JsonSerializable(typeof(PlayLiveballCommand))]
public sealed partial class PlaybackCommandJson : JsonSerializerContext;
