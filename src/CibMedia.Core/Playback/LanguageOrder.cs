namespace CibMedia.Core.Playback;

// The dubs to try, best first: the one chosen in Settings, then the box's own languages in
// the order its owner ranked them. A progressive file is picked by it; for HLS it goes to
// ExoPlayer's track selector, so Auto lands on the same dub when the playlist has it.
public static class LanguageOrder
{
    public static IReadOnlyList<string> Of(string? preferred, IEnumerable<string> deviceLanguages)
    {
        return
        [
            .. new[] { preferred ?? string.Empty }
                .Concat(deviceLanguages)
                .Select(language => language.Trim().ToLowerInvariant())
                .Where(language => language.Length > 0)
                .Distinct()
        ];
    }
}
