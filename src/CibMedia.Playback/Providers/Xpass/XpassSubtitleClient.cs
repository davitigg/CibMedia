using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using CibMedia.Playback.Models;
using CibMedia.Playback.Providers.Xpass.Models;

namespace CibMedia.Playback.Providers.Xpass;

internal sealed partial class XpassSubtitleClient(HttpClient http, XpassOptions options)
{
    private const string CachedStatus = "cached";

    private static readonly Dictionary<string, string> Languages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["georgian"] = PlaybackLanguages.Georgian,
        ["english"] = PlaybackLanguages.English,
        ["russian"] = PlaybackLanguages.Russian
    };

    // The cached tracks in a language the app ships. Upstream offers the same ~19 machine
    // translated languages on every title and none of them is Georgian, so in practice this is
    // English and Russian.
    public async Task<IReadOnlyList<PlaybackSubtitle>> GetAsync(string path, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync($"https://{options.SubtitleHost}/api/{path}", cancellationToken);

        if (response.StatusCode is HttpStatusCode.NotFound) return [];

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        // The cache is lazy and thin: most listed entries were never fetched and 404 on request.
        return Deserialize(body)?
            .Where(subtitle => string.Equals(subtitle.Status, CachedStatus, StringComparison.OrdinalIgnoreCase))
            .Where(subtitle => !string.IsNullOrWhiteSpace(subtitle.Url))
            .Select(subtitle => new
            {
                Subtitle = subtitle,
                // Language is the canonical lowercase twin of Label; prefer it and fall back.
                Language = MapLanguage(subtitle.Language ?? subtitle.Label)
            })
            .Where(entry => entry.Language is not null)
            .Select(entry => new PlaybackSubtitle(
                Label(entry.Subtitle),
                entry.Language,
                $"https://{options.SubtitleHost}{entry.Subtitle.Url}"))
            .ToList() ?? [];
    }

    private static string Label(XpassSubtitle subtitle)
    {
        var label = subtitle.Label ?? subtitle.Language;

        return string.IsNullOrWhiteSpace(label) ? "Subtitles" : label.Trim();
    }

    // Whole-name match only. A substring match would read "Bengali" as English, and upstream ships
    // Bengali on nearly every title.
    private static string? MapLanguage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        // Names are canonical today, but the stack has been seen numbering duplicates ("English1").
        var name = TrailingIndexRegex().Replace(value.Trim(), "");

        return Languages.GetValueOrDefault(name);
    }

    private static List<XpassSubtitle>? Deserialize(string body)
    {
        try
        {
            return JsonSerializer.Deserialize<List<XpassSubtitle>>(body, JsonSerializerOptions.Web);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    [GeneratedRegex(@"[\s_-]*\d+$")]
    private static partial Regex TrailingIndexRegex();
}