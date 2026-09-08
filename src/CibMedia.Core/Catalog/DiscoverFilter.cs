namespace CibMedia.Core.Catalog;

// Any is a union of genre ids, All an intersection. The API applies All last, so a caller
// supplies one or the other.
public sealed record DiscoverFilter(
    MediaKind Kind,
    IReadOnlyList<int>? GenreIdsAny = null,
    IReadOnlyList<int>? GenreIdsAll = null,
    SortOrder Sort = SortOrder.Popularity);
