using System.Text.Json.Serialization;

namespace CibMedia.Playback.Models;

// Video and subtitles inside a source are timed against the same encode; never pair them across
// sources. Headers go with every stream and subtitle request the player makes.
public sealed record PlaybackSource(
    PlaybackProvider Provider,
    IReadOnlyDictionary<string, string> Headers,
    IReadOnlyList<PlaybackStream> Streams,
    IReadOnlyList<PlaybackSubtitle> Subtitles)
{
    private const string UnnamedLabel = "Stream";

    // Best first, and no two carrying the same label.
    public IReadOnlyList<PlaybackStream> Streams
    {
        get;
        init => field = Disambiguate(value);
    } = Disambiguate(Streams);

    // Enforced here rather than in each provider so a new one cannot forget it.
    private static List<PlaybackStream> Disambiguate(IReadOnlyList<PlaybackStream> streams)
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<PlaybackStream>(streams.Count);

        foreach (var stream in streams)
        {
            var label = string.IsNullOrWhiteSpace(stream.Label) ? UnnamedLabel : stream.Label.Trim();

            var unique = label;
            for (var n = 2; !taken.Add(unique); n++) unique = $"{label} {n}";

            result.Add(unique == stream.Label ? stream : stream with { Label = unique });
        }

        return result;
    }
}
