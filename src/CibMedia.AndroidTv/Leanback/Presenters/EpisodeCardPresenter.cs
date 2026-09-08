using _Microsoft.Android.Resource.Designer;
using CibMedia.Core.Abstractions;
using CibMedia.Core.Catalog;

namespace CibMedia.AndroidTv.Leanback.Presenters;

public sealed class EpisodeCardPresenter(IImageLoader images)
    : CardPresenter<Episode>(
        images,
        ResourceConstant.Dimension.episode_card_width,
        ResourceConstant.Dimension.episode_still_height)
{
    protected override int Placeholder => ResourceConstant.Drawable.card_placeholder_episode;

    protected override string? ImageUrl(Episode item)
    {
        return item.StillUrl;
    }

    protected override (string? Title, string? Content) Labels(Episode item)
    {
        return (item.Title, item.Label);
    }

    protected override double? RatingOf(Episode item)
    {
        return item.Rating;
    }

    protected override string? BadgeOf(Episode item)
    {
        return item.RuntimeText;
    }

    protected override bool IsDimmed(Episode item)
    {
        return !item.HasAired;
    }
}
