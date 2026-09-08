using CibMedia.Core.Abstractions;
using CibMedia.Core.Catalog;
using CibMedia.Core.Presentation.Rails;

namespace CibMedia.Core.Presentation.Browse;

// The Movies and TV Shows sections: the same page as Home over a different rail table.
public sealed class BrowseViewModel(ICatalog catalog, MediaKind kind)
    : RailsViewModel<BrowseRail>(BrowseRails.For(catalog, kind))
{
    public MediaKind Kind { get; } = kind;
}
