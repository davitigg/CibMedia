namespace CibMedia.Core.Common;

// The layout is drawn for a 960dp-wide screen, but a panel may report any width for the same
// pixels: 1920px is 960dp at density 320 and 1280dp at density 240. These turn what a panel
// reports back into the width the design assumes, with the user's step applied on top.
public static class UiScale
{
    public const int DesignWidthDp = 960;

    // Guards against a panel reporting a width or density that cannot be true, which would
    // resolve to an unreadable screen with no way back to the card that set it.
    private const int MinimumDensityDpi = 80;
    private const int MaximumDensityDpi = 960;

    // The ceiling is a memory budget: card dimensions are what the presenters hand Glide, so a
    // step up is a step up in decoded bitmap area.
    public static float Factor(UiScaleStep step)
    {
        return step switch
        {
            UiScaleStep.Smaller => 0.75f,
            UiScaleStep.Small => 0.875f,
            UiScaleStep.Large => 1.125f,
            UiScaleStep.Larger => 1.25f,
            _ => 1f
        };
    }

    public static UiScaleStep Next(UiScaleStep step)
    {
        return step >= UiScaleStep.Larger ? UiScaleStep.Smaller : step + 1;
    }

    // Density is the only lever Android offers: every dp and sp, Leanback's own included, is
    // resolved through it.
    public static int DensityDpiFor(int reportedWidthDp, int reportedDensityDpi, UiScaleStep step)
    {
        if (reportedWidthDp <= 0 || reportedDensityDpi <= 0) return reportedDensityDpi;

        var wanted = (double)reportedDensityDpi * reportedWidthDp * Factor(step) / DesignWidthDp;

        return Math.Clamp((int)Math.Round(wanted), MinimumDensityDpi, MaximumDensityDpi);
    }

    // The same pixels restated against another density.
    public static int Rescale(int dp, int fromDensityDpi, int toDensityDpi)
    {
        return toDensityDpi <= 0 ? dp : (int)Math.Round((double)dp * fromDensityDpi / toDensityDpi);
    }
}
