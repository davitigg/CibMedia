using System.Text.Json.Serialization;
using CibMedia.Core.Common;

namespace CibMedia.Core.Playback;

// Everything the API offered for a title, by provider, and which stream the player is on.
public sealed record PlayableStream(
    IReadOnlyList<ProviderStreams> Providers,
    bool IsLive = false,
    int ProviderIndex = 0,
    int StreamIndex = 0,
    long ResumePositionMs = 0)
{
    [JsonIgnore]
    public ProviderStreams Source => Providers[ProviderIndex];

    [JsonIgnore]
    public StreamEntry Current => Source.Streams[StreamIndex];

    [JsonIgnore]
    public IReadOnlyList<string> ProviderNames => [.. Providers.Select(provider => provider.Provider)];

    // By name, so a Settings default or an Intent extra can ask for one; unchanged when the
    // title has no such provider. Lands on the provider's first stream.
    public PlayableStream ChooseProvider(string? provider)
    {
        var index = IndexOfProvider(provider);

        return index < 0 ? this : this with { ProviderIndex = index, StreamIndex = 0 };
    }

    // By label, which no two streams of a provider share; unchanged when it has no such stream.
    public PlayableStream ChooseStream(string label)
    {
        var index = IndexOfLabel(Source, label);

        return index < 0 ? this : this with { StreamIndex = index };
    }

    // The first dub in the viewer's order that the provider has; unchanged when it has none of
    // them, or when the stream on names no language and the dub is ExoPlayer's to choose.
    public PlayableStream PreferLanguages(IReadOnlyList<string> order)
    {
        if (Current.Language is null) return this;

        foreach (var language in order)
        {
            var index = Source.Streams.FindIndex(stream => Speaks(stream, language));
            if (index >= 0) return this with { StreamIndex = index };
        }

        return this;
    }

    // The stream to try after this one failed: the provider's others once each, in order from
    // the one after it and round, with the same language ahead of the rest. Null when every
    // other has been tried.
    public PlayableStream? NextFallback(IReadOnlySet<string> tried)
    {
        var streams = Source.Streams;

        var candidates = Enumerable.Range(1, Math.Max(streams.Count - 1, 0))
            .Select(offset => (StreamIndex + offset) % streams.Count)
            .Where(index => !tried.Contains(streams[index].Label))
            .ToList();

        if (candidates.Count == 0) return null;

        var index = Current.Language is { } language
            ? candidates.FirstOrDefault(candidate => Speaks(streams[candidate], language), candidates[0])
            : candidates[0];

        return this with { StreamIndex = index };
    }

    private static bool Speaks(StreamEntry stream, string language)
    {
        return string.Equals(stream.Language, language, StringComparison.OrdinalIgnoreCase);
    }

    private static int IndexOfLabel(ProviderStreams source, string label)
    {
        return source.Streams.FindIndex(
            stream => string.Equals(stream.Label, label, StringComparison.OrdinalIgnoreCase));
    }

    private int IndexOfProvider(string? provider)
    {
        return provider is null
            ? -1
            : Providers.FindIndex(
                candidate => string.Equals(candidate.Provider, provider, StringComparison.OrdinalIgnoreCase));
    }
}
