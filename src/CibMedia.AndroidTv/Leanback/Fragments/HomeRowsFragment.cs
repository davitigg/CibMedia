using _Microsoft.Android.Resource.Designer;
using CibMedia.Core.Presentation.Home;
using Microsoft.Extensions.DependencyInjection;

namespace CibMedia.AndroidTv.Leanback.Fragments;

public sealed class HomeRowsFragment : RailsFragment<HomeRail, HomeViewModel>
{
    private bool _resumedBefore;

    protected override string StateKey => "home";

    protected override HomeViewModel CreateViewModel(IServiceProvider services)
    {
        return ActivatorUtilities.CreateInstance<HomeViewModel>(services);
    }

    // Continue Watching is re-read on every arrival except the very first, when the initial
    // load is already asking. Back from the player resumes this fragment; back from another
    // section builds a new one over a kept view model, which is what ReusedViewModel catches.
    public override void OnResume()
    {
        base.OnResume();

        if (_resumedBefore || ReusedViewModel) _ = ViewModel?.ReloadContinueWatchingAsync();

        _resumedBefore = true;
    }

    protected override int TitleOf(HomeRail rail)
    {
        return rail switch
        {
            HomeRail.ContinueWatching => ResourceConstant.String.rail_continue,
            HomeRail.Trending => ResourceConstant.String.rail_trending,
            HomeRail.PopularMovies => ResourceConstant.String.rail_popular_movies,
            HomeRail.TopRatedMovies => ResourceConstant.String.rail_top_rated_movies,
            HomeRail.PopularTvShows => ResourceConstant.String.rail_popular_tv,
            _ => ResourceConstant.String.rail_top_rated_tv
        };
    }
}
