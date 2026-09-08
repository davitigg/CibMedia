using _Microsoft.Android.Resource.Designer;
using Android.Util;
using Android.Views;
using AndroidX.Leanback.Widget;
using CibMedia.AndroidTv.Design;

namespace CibMedia.AndroidTv.Leanback.Support;

// A line of text over the top-right corner of a card's artwork. BaseCardView lays its
// children out as stacked regions, so the artwork is moved into a FrameLayout of its own to
// give the badge something to overlap. Installed on the first card that asks for one, since
// most carry none.
public static class CardBadge
{
    public static void Set(ImageCardView card, string? text)
    {
        if (card.FindViewById<TextView>(ResourceConstant.Id.card_badge) is not { } badge)
        {
            if (string.IsNullOrEmpty(text)) return;

            badge = Install(card);
        }

        // ImageCardView hides its artwork until setMainImage is called, and un-hides it by
        // forcing every view in its main region visible on measure. Inside the frame the
        // artwork is no longer one of those views.
        card.MainImageView!.Visibility = ViewStates.Visible;

        badge.Text = text;
        badge.Visibility = string.IsNullOrEmpty(text) ? ViewStates.Gone : ViewStates.Visible;
    }

    private static TextView Install(ImageCardView card)
    {
        var context = card.Context!;
        var image = card.MainImageView!;
        var index = card.IndexOfChild(image);

        // The artwork's layout params carry the region marker BaseCardView sorts children by.
        var region = image.LayoutParameters;

        card.RemoveViewAt(index);

        var frame = new FrameLayout(context);
        frame.AddView(
            image,
            new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.MatchParent));

        var badge = new TextView(context) { Id = ResourceConstant.Id.card_badge };
        badge.SetBackgroundResource(ResourceConstant.Drawable.card_badge_bg);
        badge.SetTextColor(Tokens.TextPrimary(context));
        badge.SetTextSize(ComplexUnitType.Px, Dimen(card, ResourceConstant.Dimension.card_badge_text_size));
        badge.SetPadding(
            Dimen(card, ResourceConstant.Dimension.card_badge_padding_horizontal),
            Dimen(card, ResourceConstant.Dimension.card_badge_padding_vertical),
            Dimen(card, ResourceConstant.Dimension.card_badge_padding_horizontal),
            Dimen(card, ResourceConstant.Dimension.card_badge_padding_vertical));

        var margin = Dimen(card, ResourceConstant.Dimension.card_badge_margin);
        var placement = new FrameLayout.LayoutParams(
            ViewGroup.LayoutParams.WrapContent,
            ViewGroup.LayoutParams.WrapContent)
        {
            Gravity = GravityFlags.Top | GravityFlags.End
        };
        placement.SetMargins(margin, margin, margin, margin);

        frame.AddView(badge, placement);
        card.AddView(frame, index, region);

        return badge;
    }

    private static int Dimen(View view, int id)
    {
        return view.Resources!.GetDimensionPixelSize(id);
    }
}
