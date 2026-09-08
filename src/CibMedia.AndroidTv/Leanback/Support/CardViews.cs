using Android.Content;
using Android.Graphics;
using AndroidX.Leanback.Widget;

namespace CibMedia.AndroidTv.Leanback.Support;

// Every card in the app is built here, so label styling and focus treatment cannot drift.
public static class CardViews
{
    public static ImageCardView Create(Context context, int width, int height)
    {
        var card = new ImageCardView(context)
        {
            Focusable = true,
            FocusableInTouchMode = true,
            CardType = BaseCardView.CardTypeInfoUnder,
            InfoVisibility = BaseCardView.CardRegionVisibleAlways
        };

        card.SetMainImageDimensions(width, height);

        // As well as lb_basic_card_info_bg_color: the resource is the style default and this
        // is what a built ImageCardView actually keeps.
        card.SetInfoAreaBackgroundColor(Color.Transparent);

        return card;
    }
}
