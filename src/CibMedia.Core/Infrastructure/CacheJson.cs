using System.Text.Json.Serialization;
using CibMedia.Core.Catalog;
using CibMedia.Core.Common;
using TMDbLib.Objects.General;

namespace CibMedia.Core.Infrastructure;

// Everything that goes into the cache store. Source-generated because reflection-based
// serialisation silently produces empty objects under TrimMode=full, in Release only.
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Page<MediaCard>))]
[JsonSerializable(typeof(MovieDetails))]
[JsonSerializable(typeof(TvShowDetails))]
[JsonSerializable(typeof(SeasonDetails))]
[JsonSerializable(typeof(TMDbConfig))]
public sealed partial class CacheJson : JsonSerializerContext;
