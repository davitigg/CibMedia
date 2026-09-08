using Android.Content;
using AndroidX.Core.OS;

namespace CibMedia.AndroidTv.Platform;

// The box's languages in the order its owner ranked them, as the two-letter codes the API uses.
public static class DeviceLanguages
{
    public static IReadOnlyList<string> Codes(Context context)
    {
        if (context.Resources?.Configuration is not { } configuration) return [];

        if (ConfigurationCompat.GetLocales(configuration) is not { } locales) return [];

        var codes = new List<string>(locales.Size());

        for (var index = 0; index < locales.Size(); index++)
        {
            if (locales.Get(index)?.Language is { Length: > 0 } code) codes.Add(code);
        }

        return codes;
    }
}
