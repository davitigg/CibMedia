using CibMedia.Core.Abstractions;

namespace CibMedia.AndroidTv.Leanback.Presenters;

// Held across Activities so each details page does not re-resolve card metrics from cold; a
// presenter holds no Context. No shared view pool: each rail draws through a different
// presenter, and a pool keyed by view type would hand a cast card to the episodes rail.
public sealed class DetailPresenters(IImageLoader images)
{
    public SeasonCardPresenter Seasons { get; } = new(images);

    public EpisodeCardPresenter Episodes { get; } = new(images);

    public CastCardPresenter Cast { get; } = new(images);

    public MediaCardPresenter Media { get; } = new(images);
}
