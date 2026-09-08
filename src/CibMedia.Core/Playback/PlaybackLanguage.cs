using System.Globalization;

namespace CibMedia.Core.Playback;

// A dub the API can name. Known ones are named directly rather than through CultureInfo:
// .NET for Android ships trimmed ICU data, and CultureInfo.GetCultureInfo("ka") throws
// on-device while resolving fine on a desktop.
public sealed record PlaybackLanguage(string Code, string Name)
{
    public static readonly IReadOnlyList<PlaybackLanguage> Known =
    [
        new("ka", "Georgian"),
        new("en", "English"),
        new("ru", "Russian")
    ];

    // Language only, to read the same way as Media3's own track dialog for HLS.
    public static string NameOf(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return "Stream";

        if (Known.FirstOrDefault(known => string.Equals(known.Code, code, StringComparison.OrdinalIgnoreCase)) is { } match)
            return match.Name;

        try
        {
            return CultureInfo.GetCultureInfo(code).EnglishName;
        }
        catch (CultureNotFoundException)
        {
            return code.ToUpperInvariant();
        }
    }
}
