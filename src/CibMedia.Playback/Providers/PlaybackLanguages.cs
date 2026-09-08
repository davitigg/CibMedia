namespace CibMedia.Playback.Providers;

// The app ships Georgian, English and Russian. Anything else a provider offers is dropped rather
// than surfaced as a choice the UI has no wording for.
internal static class PlaybackLanguages
{
    public const string Georgian = "ka";
    public const string English = "en";
    public const string Russian = "ru";

    // Preferred order.
    private static readonly (string Code, string Name)[] Supported =
    [
        (Georgian, "Georgian"),
        (English, "English"),
        (Russian, "Russian")
    ];

    public static bool IsSupported(string? language)
    {
        return Rank(language) >= 0;
    }

    // -1 when the language is not shipped.
    public static int Rank(string? language)
    {
        return language is null
            ? -1
            : Array.FindIndex(
                Supported,
                entry => string.Equals(entry.Code, language, StringComparison.OrdinalIgnoreCase));
    }

    public static string? Name(string? language)
    {
        var rank = Rank(language);

        return rank < 0 ? null : Supported[rank].Name;
    }
}