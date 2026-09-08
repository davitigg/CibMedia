using CibMedia.Core.Abstractions;
using CibMedia.Core.Catalog;
using CibMedia.Core.Presentation.Rails;

namespace CibMedia.Core.Presentation.Browse;

// TMDb keeps separate genre id spaces per media type: 16 and 35 mean the same on both sides,
// but a film is 28 Action where a series is 10759 Action & Adventure.
//
// Each section opens on its own trending rail, not an unfiltered popularity one: that query
// is exactly Home's Popular rail. Genres match on Any except where noted, because a union
// fills a rail and an intersection usually does not.
public static class BrowseRails
{
    private static readonly Spec[] Movies =
    [
        new(BrowseRail.TrendingWeek, Trending: true),

        // The one intersection: Any[28,12] repeats most of Popular Movies, All[28,12] still has
        // thousands of titles behind it.
        new(BrowseRail.ActionAdventure, All: [28, 12]),

        new(BrowseRail.Comedy, [35]),
        new(BrowseRail.Drama, [18]),
        new(BrowseRail.CrimeThriller, [80, 53, 9648]),
        new(BrowseRail.SciFiFantasy, [878, 14]),
        new(BrowseRail.Horror, [27]),
        new(BrowseRail.Romance, [10749]),
        new(BrowseRail.FamilyAnimation, [16, 10751])
    ];

    private static readonly Spec[] TvShows =
    [
        new(BrowseRail.TrendingWeek, Trending: true),
        new(BrowseRail.Drama, [18]),
        new(BrowseRail.Comedy, [35]),
        new(BrowseRail.CrimeMystery, [80, 9648]),
        new(BrowseRail.ActionAdventure, [10759]),
        new(BrowseRail.SciFiFantasy, [10765]),
        new(BrowseRail.AnimationKids, [16, 10762, 10751]),
        new(BrowseRail.RealityTalk, [10764, 10767])
    ];

    // Top to bottom, the first being the rail the section opens on.
    public static IReadOnlyList<Rail<BrowseRail>> For(ICatalog catalog, MediaKind kind)
    {
        return [.. Table(kind).Select(spec => spec.ToRail(catalog, kind))];
    }

    private static Spec[] Table(MediaKind kind)
    {
        return kind == MediaKind.Movie ? Movies : TvShows;
    }

    private sealed record Spec(
        BrowseRail Key,
        int[]? Any = null,
        int[]? All = null,
        bool Trending = false)
    {
        public Rail<BrowseRail> ToRail(ICatalog catalog, MediaKind kind)
        {
            if (Trending)
                return new Rail<BrowseRail>(
                    Key,
                    (page, ct) => catalog.GetTrendingAsync(kind, TrendingWindow.Week, page, ct));

            var filter = new DiscoverFilter(kind, Any, All);

            return new Rail<BrowseRail>(Key, (page, ct) => catalog.DiscoverAsync(filter, page, ct));
        }
    }
}
