namespace CibMedia.Core.Playback;

// What one provider offers for a title. Headers and subtitles belong to the provider's own
// encodes and are never borrowed across providers. Streams are best first, in the API's order,
// and no two carry the same label.
public sealed record ProviderStreams(
    string Provider,
    IReadOnlyDictionary<string, string> Headers,
    IReadOnlyList<Subtitle> Subtitles,
    IReadOnlyList<StreamEntry> Streams);
