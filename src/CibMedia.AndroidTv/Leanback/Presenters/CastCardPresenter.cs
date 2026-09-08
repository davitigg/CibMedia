using _Microsoft.Android.Resource.Designer;
using CibMedia.Core.Abstractions;
using CibMedia.Core.Catalog;

namespace CibMedia.AndroidTv.Leanback.Presenters;

public sealed class CastCardPresenter(IImageLoader images)
    : CardPresenter<CastMember>(
        images,
        ResourceConstant.Dimension.cast_card_width,
        ResourceConstant.Dimension.cast_photo_height)
{
    protected override int Placeholder => ResourceConstant.Drawable.card_placeholder_person;

    protected override string? ImageUrl(CastMember item)
    {
        return item.PhotoUrl;
    }

    protected override (string? Title, string? Content) Labels(CastMember item)
    {
        return (item.Name, item.Character);
    }
}
