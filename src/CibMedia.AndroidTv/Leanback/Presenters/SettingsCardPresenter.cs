using _Microsoft.Android.Resource.Designer;
using Android.Views;
using AndroidX.Leanback.Widget;
using CibMedia.AndroidTv.Leanback.Support;
using Object = Java.Lang.Object;

namespace CibMedia.AndroidTv.Leanback.Presenters;

// The same ImageCardView as every other card, with a local icon instead of artwork.
public sealed class SettingsCardPresenter : Presenter
{
    public override ViewHolder OnCreateViewHolder(ViewGroup? parent)
    {
        var context = parent!.Context!;

        var card = CardViews.Create(
            context,
            context.Resources!.GetDimensionPixelSize(ResourceConstant.Dimension.settings_card_width),
            context.Resources.GetDimensionPixelSize(ResourceConstant.Dimension.settings_card_height));

        // Center, not fitCenter: the icon is authored at the size it should appear.
        card.MainImageView!.SetScaleType(ImageView.ScaleType.Center);
        card.MainImageView.SetBackgroundResource(ResourceConstant.Drawable.settings_card_tile);

        return new ViewHolder(card);
    }

    public override void OnBindViewHolder(ViewHolder? viewHolder, Object? item)
    {
        if (viewHolder?.View is not ImageCardView card ||
            JavaRef.Unwrap<SettingsAction>(item) is not { } action) return;

        card.TitleText = action.Title;
        card.ContentText = action.Subtitle;
        card.MainImageView!.SetImageResource(action.IconResourceId);
    }

    public override void OnUnbindViewHolder(ViewHolder? viewHolder)
    {
        if (viewHolder?.View is ImageCardView card) card.MainImageView!.SetImageDrawable(null);
    }
}
