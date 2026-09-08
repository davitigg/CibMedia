using _Microsoft.Android.Resource.Designer;
using Android.Content;
using Android.Graphics;
using AndroidX.Core.Content;

namespace CibMedia.AndroidTv.Design;

// The palette and the details header's artwork sizes. Type scale, spacing, focus treatment
// and the browse chrome come from Leanback's own resources, overridden by name under
// Resources/values.
public static class Tokens
{
    // The scrim over a card's artwork for a season not on show or an episode not yet aired.
    public const int DimmedScrimAlpha = 140;

    public static Color BgBase(Context context)
    {
        return Color(context, ResourceConstant.Color.bg_base);
    }

    public static Color BgSurface(Context context)
    {
        return Color(context, ResourceConstant.Color.bg_surface);
    }

    public static Color BgNav(Context context)
    {
        return Color(context, ResourceConstant.Color.bg_nav);
    }

    public static Color BgPlaceholder(Context context)
    {
        return Color(context, ResourceConstant.Color.bg_placeholder);
    }

    public static Color TextPrimary(Context context)
    {
        return Color(context, ResourceConstant.Color.text_primary);
    }

    public static Color TextMuted(Context context)
    {
        return Color(context, ResourceConstant.Color.text_muted);
    }

    public static Color AccentGold(Context context)
    {
        return Color(context, ResourceConstant.Color.accent_gold);
    }

    public static int CardWidth(Context context)
    {
        return Dimen(context, ResourceConstant.Dimension.card_width);
    }

    // The details header's poster, taller than a card's because Leanback gives it the full
    // height of the overview row.
    public static int CardFocusedHeight(Context context)
    {
        return Dimen(context, ResourceConstant.Dimension.card_focused_height);
    }

    public static int HeroHeight(Context context)
    {
        return Dimen(context, ResourceConstant.Dimension.hero_height);
    }

    public static int HeroBackdropWidth(Context context)
    {
        return Dimen(context, ResourceConstant.Dimension.hero_backdrop_width);
    }

    private static Color Color(Context context, int id)
    {
        return new Color(ContextCompat.GetColor(context, id));
    }

    private static int Dimen(Context context, int id)
    {
        return context.Resources!.GetDimensionPixelSize(id);
    }
}
