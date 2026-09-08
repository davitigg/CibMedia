using System.Text.RegularExpressions;
using CibMedia.Playback.Models;
using CibMedia.Playback.Providers.VideoDb.Models;

namespace CibMedia.Playback.Providers.VideoDb;

internal static partial class VideoDbPlaylistParser
{
    private const string HlsMarker = "/cdn/hls/";

    private static readonly Dictionary<string, string> AudioLanguages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ქართულად"] = PlaybackLanguages.Georgian,
        ["ინგლისურად"] = PlaybackLanguages.English,
        ["რუსულად"] = PlaybackLanguages.Russian
    };

    // Labels arrive as an ISO 639-2 code with a track index, for example "ENG_9". Only codes for
    // the languages the app ships are listed; anything else is dropped.
    private static readonly Dictionary<string, string> SubtitleLanguages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["eng"] = PlaybackLanguages.English,
        ["rus"] = PlaybackLanguages.Russian
    };

    // The shape is decided per entry by the catalogue and cannot be forced, so the value itself is
    // the only reliable signal for which branch to take.
    public static IReadOnlyList<PlaybackStream> ParseStreams(string? file)
    {
        if (string.IsNullOrWhiteSpace(file)) return [];

        return file.Contains(HlsMarker, StringComparison.Ordinal)
            // Nothing to name it by; PlaybackSource fills the label in.
            ? [new HlsStream(file, string.Empty)]
            : ParseProgressiveStreams(file);
    }

    public static IReadOnlyList<PlaybackSubtitle> ParseSubtitles(IReadOnlyList<VideoDbSubtitle>? subtitles)
    {
        return subtitles?
            .Where(subtitle => !string.IsNullOrWhiteSpace(subtitle.File) && !string.IsNullOrWhiteSpace(subtitle.Label))
            .Select(subtitle => (subtitle.Label, Language: MapSubtitleLanguage(subtitle.Label), subtitle.File))
            .Where(subtitle => subtitle.Language is not null)
            .Select(subtitle => new PlaybackSubtitle(subtitle.Label!.Trim(), subtitle.Language, subtitle.File!))
            .ToList() ?? [];
    }

    private static List<PlaybackStream> ParseProgressiveStreams(string file)
    {
        var parsed = new List<(string? Quality, string Language, string Url)>();

        foreach (var bucket in file.Split(','))
        {
            var match = BucketRegex().Match(bucket);
            if (!match.Success) continue;

            var tag = match.Groups["quality"].Value.Trim();
            var quality = tag.Length is 0 ? null : tag;

            foreach (Match pair in PairRegex().Matches(match.Groups["rest"].Value))
            {
                var language = MapAudioLanguage(pair.Groups["language"].Value);
                if (language is null) continue;

                parsed.Add((quality, language, pair.Groups["url"].Value));
            }
        }

        return parsed
            .OrderBy(stream => PlaybackLanguages.Rank(stream.Language))
            .ThenBy(stream => QualityRank(stream.Quality))
            // The quality buckets routinely hold the same file twice; keeping both would offer a
            // choice that changes nothing.
            .DistinctBy(stream => stream.Url)
            .Select(PlaybackStream (stream) => new ProgressiveStream(
                stream.Url,
                $"{PlaybackLanguages.Name(stream.Language)} {stream.Quality}".Trim(),
                stream.Quality,
                stream.Language))
            .ToList();
    }

    private static string? MapAudioLanguage(string label)
    {
        return AudioLanguages.GetValueOrDefault(label.Trim());
    }

    private static string? MapSubtitleLanguage(string? label)
    {
        if (string.IsNullOrWhiteSpace(label)) return null;

        return SubtitleLanguages.GetValueOrDefault(SubtitleIndexSuffixRegex().Replace(label.Trim(), string.Empty));
    }

    private static int QualityRank(string? quality)
    {
        return quality?.ToUpperInvariant() switch
        {
            "HD" => 0,
            "SD" => 2,
            _ => 1
        };
    }

    [GeneratedRegex(@"^\[(?<quality>[^\]]+)\](?<rest>.*)$")]
    private static partial Regex BucketRegex();

    [GeneratedRegex(@"\{(?<language>[^}]*)\}(?<url>[^;]+)")]
    private static partial Regex PairRegex();

    [GeneratedRegex(@"_\d+$")]
    private static partial Regex SubtitleIndexSuffixRegex();
}