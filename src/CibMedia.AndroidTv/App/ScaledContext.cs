using Android.Content;
using Android.Content.Res;
using CibMedia.Core.Common;
using CibMedia.AndroidTv.Platform;

namespace CibMedia.AndroidTv.App;

// Every Activity attaches its base context through here, the last point before the theme and
// any dimension is resolved.
public static class ScaledContext
{
    public static Context? Wrap(Context? context)
    {
        if (context?.Resources?.Configuration is not { } reported) return context;

        var density = UiScale.DensityDpiFor(
            reported.ScreenWidthDp,
            reported.DensityDpi,
            new PreferencesUiScale(context).Step);

        if (density == reported.DensityDpi) return context;

        // The dp figures do not follow DensityDpi on their own, and a Configuration that
        // disagrees with itself picks resource qualifiers for a screen that is not there.
        var configuration = new Configuration(reported)
        {
            DensityDpi = density,
            ScreenWidthDp = UiScale.Rescale(reported.ScreenWidthDp, reported.DensityDpi, density),
            ScreenHeightDp = UiScale.Rescale(reported.ScreenHeightDp, reported.DensityDpi, density),
            SmallestScreenWidthDp =
                UiScale.Rescale(reported.SmallestScreenWidthDp, reported.DensityDpi, density)
        };

        return context.CreateConfigurationContext(configuration);
    }
}
