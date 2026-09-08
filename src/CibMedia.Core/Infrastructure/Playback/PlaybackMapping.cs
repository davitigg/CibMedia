using CibMedia.Core.Common;
using CibMedia.Core.Playback;
using CibMedia.Playback.Models;

namespace CibMedia.Core.Infrastructure.Playback;

// The only place that knows what a resolver answers with.
internal static class PlaybackMapping
{
    // A provider is a name above this line, never one of the resolver's own values.
    public static IReadOnlyList<string> ToNames(this IReadOnlyList<PlaybackProvider> providers)
    {
        return [.. providers.Select(provider => provider.ToString())];
    }

    // The one failure a details page is allowed to read as "unavailable".
    public static UpstreamException NoStream()
    {
        return new UpstreamException(AppErrorKind.NotFound, "No playable stream was found for this title.");
    }

    // Every provider that answered, in its order, each with every stream it offers, best first.
    // The first stream is what plays unless something chooses otherwise.
    public static PlayableStream ToPlayableStream(this ResolvedPlayback playback)
    {
        var providers = playback.Sources
            .Select(ProviderOf)
            .Where(provider => provider.Streams.Count > 0)
            .ToArray();

        if (providers.Length == 0) throw NoStream();

        return new PlayableStream(providers, playback.MediaType is PlaybackMediaType.Livestream);
    }

    private static ProviderStreams ProviderOf(PlaybackSource source)
    {
        var subtitles = source.Subtitles
            .Select(subtitle => new Subtitle(subtitle.Label, subtitle.Url, MimeTypeFor(subtitle.Url), subtitle.Language))
            .ToArray();

        var streams = source.Streams
            .Select(EntryOf)
            .OfType<StreamEntry>()
            .ToArray();

        return new ProviderStreams(
            source.Provider.ToString(),
            new Dictionary<string, string>(source.Headers),
            subtitles,
            streams);
    }

    // A kind this client has no player for is left out rather than guessed at.
    private static StreamEntry? EntryOf(PlaybackStream stream)
    {
        return stream switch
        {
            HlsStream hls => new StreamEntry(hls.Url, true, hls.Label, null),
            ProgressiveStream file => new StreamEntry(file.Url, false, file.Label, LanguageOf(file.Language)),
            _ => null
        };
    }

    private static string? LanguageOf(string? language)
    {
        return string.IsNullOrWhiteSpace(language) ? null : language.ToLowerInvariant();
    }

    // ExoPlayer needs the subtitle format up front; without a MIME type the track silently
    // fails to load rather than erroring.
    private static string? MimeTypeFor(string url)
    {
        var extension = url.Contains('?') ? url[..url.IndexOf('?')] : url;

        return extension switch
        {
            _ when extension.EndsWith(".vtt", StringComparison.OrdinalIgnoreCase) => "text/vtt",
            _ when extension.EndsWith(".srt", StringComparison.OrdinalIgnoreCase) => "application/x-subrip",
            _ when extension.EndsWith(".ttml", StringComparison.OrdinalIgnoreCase)
                || extension.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) => "application/ttml+xml",
            _ when extension.EndsWith(".ssa", StringComparison.OrdinalIgnoreCase)
                || extension.EndsWith(".ass", StringComparison.OrdinalIgnoreCase) => "text/x-ssa",
            _ => null
        };
    }
}
