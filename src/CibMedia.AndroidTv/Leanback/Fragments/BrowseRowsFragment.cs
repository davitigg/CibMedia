using _Microsoft.Android.Resource.Designer;
using CibMedia.Core.Catalog;
using CibMedia.Core.Presentation.Browse;
using Microsoft.Extensions.DependencyInjection;

namespace CibMedia.AndroidTv.Leanback.Fragments;

// The Movies and TV Shows sections: Home with a different rail table behind it.
public sealed class BrowseRowsFragment : RailsFragment<BrowseRail, BrowseViewModel>
{
    private const string ArgKind = "kind";

    protected override string StateKey => $"browse:{Kind}";

    private MediaKind Kind => (MediaKind)(Arguments?.GetInt(ArgKind) ?? 0);

    public static BrowseRowsFragment ForMovies()
    {
        return For(MediaKind.Movie);
    }

    public static BrowseRowsFragment ForTvShows()
    {
        return For(MediaKind.TvShow);
    }

    // In the arguments bundle, not a field: Android recreates a fragment through its
    // parameterless constructor after a configuration change.
    private static BrowseRowsFragment For(MediaKind kind)
    {
        var fragment = new BrowseRowsFragment();
        var args = new Bundle();
        args.PutInt(ArgKind, (int)kind);
        fragment.Arguments = args;

        return fragment;
    }

    protected override BrowseViewModel CreateViewModel(IServiceProvider services)
    {
        return ActivatorUtilities.CreateInstance<BrowseViewModel>(services, Kind);
    }

    protected override int TitleOf(BrowseRail rail)
    {
        return rail switch
        {
            BrowseRail.TrendingWeek => ResourceConstant.String.rail_trending_week,
            BrowseRail.ActionAdventure => ResourceConstant.String.rail_action_adventure,
            BrowseRail.Comedy => ResourceConstant.String.rail_comedy,
            BrowseRail.Drama => ResourceConstant.String.rail_drama,
            BrowseRail.CrimeThriller => ResourceConstant.String.rail_crime_thriller,
            BrowseRail.CrimeMystery => ResourceConstant.String.rail_crime_mystery,
            BrowseRail.SciFiFantasy => ResourceConstant.String.rail_scifi_fantasy,
            BrowseRail.Horror => ResourceConstant.String.rail_horror,
            BrowseRail.Romance => ResourceConstant.String.rail_romance,
            BrowseRail.FamilyAnimation => ResourceConstant.String.rail_family_animation,
            BrowseRail.AnimationKids => ResourceConstant.String.rail_animation_kids,
            _ => ResourceConstant.String.rail_reality_talk
        };
    }
}
