using System.Text.Json.Serialization;
using CibMedia.Core.Playback;

namespace CibMedia.AndroidTv.Platform;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(WatchProgress[]))]
public sealed partial class HistoryJson : JsonSerializerContext;
