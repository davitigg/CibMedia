using _Microsoft.Android.Resource.Designer;
using CibMedia.Core.Abstractions;
using CibMedia.Core.Catalog;
using CibMedia.AndroidTv.Design;

namespace CibMedia.AndroidTv.Leanback.Presenters;

public sealed class SeasonCardPresenter(IImageLoader images)
    : CardPresenter<SeasonRef>(
        images,
        ResourceConstant.Dimension.season_card_width,
        ResourceConstant.Dimension.season_poster_height)
{
    // Lives outside the seasons list, so the row is re-notified when it changes rather than
    // resubmitted.
    public int SelectedSeason { get; set; }

    protected override string? ImageUrl(SeasonRef item)
    {
        return item.PosterUrl;
    }

    protected override (string? Title, string? Content) Labels(SeasonRef item)
    {
        return (item.Name, MetaText.Join($"{item.EpisodeCount} ep", item.Year?.ToString()));
    }

    // The episodes rail below shows one season, so the others are dimmed back.
    protected override bool IsDimmed(SeasonRef item)
    {
        return item.SeasonNumber != SelectedSeason;
    }
}
