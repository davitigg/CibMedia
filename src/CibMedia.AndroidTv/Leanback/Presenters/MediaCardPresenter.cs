using _Microsoft.Android.Resource.Designer;
using CibMedia.Core.Abstractions;
using CibMedia.Core.Catalog;

namespace CibMedia.AndroidTv.Leanback.Presenters;

public sealed class MediaCardPresenter(IImageLoader images)
    : CardPresenter<MediaCard>(
        images,
        ResourceConstant.Dimension.card_width,
        ResourceConstant.Dimension.card_poster_height)
{
    protected override string? ImageUrl(MediaCard item)
    {
        return item.PosterUrl;
    }

    protected override (string? Title, string? Content) Labels(MediaCard item)
    {
        return (item.Title, item.Year?.ToString());
    }

    protected override double? RatingOf(MediaCard item)
    {
        return item.Rating;
    }

    // Only Continue Watching cards carry a season and episode.
    protected override string? BadgeOf(MediaCard item)
    {
        return item.SeasonNumber is { } season && item.EpisodeNumber is { } episode
            ? Episode.LabelFor(season, episode)
            : null;
    }
}
