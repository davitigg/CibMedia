using _Microsoft.Android.Resource.Designer;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using AndroidX.Leanback.Widget;
using CibMedia.Core.Abstractions;
using CibMedia.AndroidTv.Design;
using CibMedia.AndroidTv.Leanback.Support;
using Object = Java.Lang.Object;

namespace CibMedia.AndroidTv.Leanback.Presenters;

// Artwork with its labels underneath; the zoom comes from the row presenter. The artwork size
// is a pair of dimension ids, so the same card at a second size is a constructor argument.
public abstract class CardPresenter<TItem>(IImageLoader images, int widthDimen, int heightDimen)
    : Presenter
    where TItem : class
{
    private (int Width, int Height)? _size;

    protected abstract string? ImageUrl(TItem item);

    // Rating is separate so the base can put the star and score in gold.
    protected abstract (string? Title, string? Content) Labels(TItem item);

    protected virtual double? RatingOf(TItem item)
    {
        return null;
    }

    protected virtual bool IsDimmed(TItem item)
    {
        return false;
    }

    // Text over the top-right of the artwork.
    protected virtual string? BadgeOf(TItem item)
    {
        return null;
    }

    // Behind the image view for the card's whole life: the loading state and the no-artwork
    // state at once, which a poster covers exactly.
    protected virtual int Placeholder => ResourceConstant.Drawable.card_placeholder_movie;

    public override ViewHolder OnCreateViewHolder(ViewGroup? parent)
    {
        var context = parent!.Context!;
        var (width, height) = ImageSize(context);

        var card = CardViews.Create(context, width, height);
        var image = card.MainImageView!;
        image.SetBackgroundResource(Placeholder);

        // Dimming is a scrim over the artwork alone rather than alpha on the card: below full
        // alpha a view fades each drawing operation in turn, so the placeholder shows through
        // the poster, and the labels would go back with it.
        var scrim = new ColorDrawable(Color.Black);
        scrim.SetAlpha(0);
        image.Foreground = scrim;

        return new ViewHolder(card);
    }

    public override void OnBindViewHolder(ViewHolder? viewHolder, Object? item)
    {
        if (viewHolder?.View is not ImageCardView card || JavaRef.Unwrap<TItem>(item) is not { } value) return;

        var (title, content) = Labels(value);
        var (width, height) = ImageSize(card.Context!);
        var image = card.MainImageView!;

        card.TitleText = title;
        card.ContentTextFormatted = MetaText.Highlighted(card.Context!, MetaText.Rating(RatingOf(value)), content);
        image.Foreground!.SetAlpha(IsDimmed(value) ? Tokens.DimmedScrimAlpha : 0);

        CardBadge.Set(card, BadgeOf(value));

        images.Load(ImageUrl(value), image, width, height);
    }

    public override void OnUnbindViewHolder(ViewHolder? viewHolder)
    {
        if (viewHolder?.View is not ImageCardView card) return;

        images.Cancel(card.MainImageView!);
        card.BadgeImage = null;
        card.MainImage = null;
    }

    // For when the presenter outlives the Activity that resolved the metrics; a display size
    // change is the one thing that moves them.
    public void Forget()
    {
        _size = null;
    }

    // Resolved once: each lookup is an AssetManager call behind a lock, and every card asks.
    private (int Width, int Height) ImageSize(Context context)
    {
        return _size ??= (context.Resources!.GetDimensionPixelSize(widthDimen),
            context.Resources.GetDimensionPixelSize(heightDimen));
    }
}
