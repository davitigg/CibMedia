using System.Text.Json;
using System.Text.RegularExpressions;

namespace CibMedia.Playback.Providers.Liveball;

// Two handoff shapes are live at once: a page with several feeds ships them all in one map and
// calls _xrq(s.t) from its tab handler, a page with one inlines a single _xrq("...") call. A page
// with neither has no broadcast, which is the ordinary case for a fixture.
internal static partial class LiveballPage
{
    public static IReadOnlyList<string> ReadPayloads(string html)
    {
        var map = StreamMapRegex().Match(html);
        if (map.Success) return ReadMappedPayloads(map.Groups[1].Value);

        var single = SinglePayloadRegex().Match(html);

        return single.Success ? [single.Groups[1].Value] : [];
    }

    // Walked as a document to keep page order, which is the order the site's own tabs use.
    private static List<string> ReadMappedPayloads(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind is not JsonValueKind.Object) return [];

            // A slot carrying "msg" in place of "t" is liveball explaining why that option is
            // unavailable, not a feed.
            return document.RootElement
                .EnumerateObject()
                .Select(slot => slot.Value.ValueKind is JsonValueKind.Object
                                && slot.Value.TryGetProperty("t", out var payload)
                                && payload.ValueKind is JsonValueKind.String
                    ? payload.GetString()
                    : null)
                .OfType<string>()
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    // The payloads are base64, so no brace inside the object can close the match early.
    [GeneratedRegex(@"window\._lbStreams\s*=\s*(\{.*?\})\s*;", RegexOptions.Singleline)]
    private static partial Regex StreamMapRegex();

    [GeneratedRegex(@"_xrq\(""([^""]+)""\)")]
    private static partial Regex SinglePayloadRegex();
}