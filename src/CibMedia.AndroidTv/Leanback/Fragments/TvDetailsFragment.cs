using _Microsoft.Android.Resource.Designer;
using AndroidX.Leanback.Widget;
using CibMedia.Core.Catalog;
using CibMedia.Core.Common;
using CibMedia.Core.Playback;
using CibMedia.Core.Presentation.TvDetail;
using CibMedia.AndroidTv.App;
using CibMedia.AndroidTv.Design;
using CibMedia.AndroidTv.Leanback.Presenters;
using CibMedia.AndroidTv.Leanback.Support;
using CibMedia.AndroidTv.Playback;
using Java.Lang;
using Microsoft.Extensions.DependencyInjection;

namespace CibMedia.AndroidTv.Leanback.Fragments;

public sealed class TvDetailsFragment : DetailsFragmentBase
{
    private StateBinding? _binding;
    private CardRow<CastMember>? _cast;
    private CardRow<Episode>? _episodes;
    private CardRow<MediaCard>? _recommendations;
    private SeasonCardPresenter? _seasonPresenter;
    private CardRow<SeasonRef>? _seasons;
    private TvDetailViewModel? _viewModel;
    private bool _loaded;
    private int _boundSeason = -1;
    private int? _openAtSeason;
    private int? _openAtEpisode;
    private bool _focusPending;

    public override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        var intent = Activity?.Intent;
        _openAtSeason = IntentExtras.GetOptionalInt(intent, IntentExtras.Season);
        _openAtEpisode = IntentExtras.GetOptionalInt(intent, IntentExtras.Episode);
        _focusPending = _openAtSeason is not null;

        var presenters = AppServices.Provider.GetRequiredService<DetailPresenters>();
        _seasonPresenter = presenters.Seasons;

        _seasons = new CardRow<SeasonRef>(
            1,
            GetString(ResourceConstant.String.rail_seasons),
            _seasonPresenter,
            season => season.SeasonNumber);

        _episodes = new CardRow<Episode>(
            2,
            GetString(ResourceConstant.String.rail_episodes),
            presenters.Episodes,
            episode => (episode.SeasonNumber, episode.EpisodeNumber));

        _cast = new CardRow<CastMember>(
            3,
            GetString(ResourceConstant.String.rail_cast),
            presenters.Cast,
            member => member.Id);

        _recommendations = new CardRow<MediaCard>(
            4,
            GetString(ResourceConstant.String.rail_recommendations),
            presenters.Media,
            card => card.Id);

        ItemViewClicked += OnItemClicked;
    }

    public override void OnDestroy()
    {
        ItemViewClicked -= OnItemClicked;

        _binding?.Dispose();
        _binding = null;
        _viewModel = null;

        base.OnDestroy();
    }

    // Coming back from the player updates the Play button's resume point immediately.
    public override void OnResume()
    {
        base.OnResume();

        if (_loaded) _ = _viewModel?.ReloadResumeAsync();

        _loaded = true;
    }

    protected override void StartLoading()
    {
        _viewModel = ActivatorUtilities.CreateInstance<TvDetailViewModel>(
            AppServices.Provider,
            MediaId.TmdbId);

        _binding = StateBinding.Bind(this, _viewModel, Render);
        _ = _viewModel.LoadAsync(_openAtSeason);
    }

    // Play from the header aims where the view model decided, so the availability check is
    // about the same episode. Picking an episode off the rail plays exactly that one.
    protected override void OnPlay(bool fromStart)
    {
        if (_viewModel?.State is not { PlayTarget: { } target } state) return;

        Nav.OpenPlayback(
            RequireContext(),
            PlaybackArgs.For(
                target,
                state.Show.ValueOrDefault,
                fromStart ? 0 : state.Resume?.ResumeMsFor(target) ?? 0,
                state.Provider));
    }

    protected override void OnNextProvider()
    {
        _viewModel?.NextProvider();
    }

    private void Render()
    {
        if (_viewModel is null || _seasons is null || _episodes is null
            || _cast is null || _recommendations is null || _seasonPresenter is null)
            return;

        var state = _viewModel.State;

        // Set before the cards are submitted so the first bind already knows which season is
        // showing.
        var seasonMoved = _boundSeason != state.SelectedSeason;

        if (seasonMoved)
        {
            _boundSeason = state.SelectedSeason;
            _seasonPresenter.SelectedSeason = state.SelectedSeason;
        }

        if (state.Show is Load<TvShowDetails>.Failed failed)
        {
            BindError(failed.Error);
            return;
        }

        if (state.Show is Load<TvShowDetails>.Ready ready)
        {
            var show = ready.Value;

            BindHeader(
                new Synopsis(show.Title, Subtitle(show), show.Overview),
                show.PosterUrl,
                show.BackdropUrl,
                PlayLabel(state.Resume, state.PlayTarget),
                ResumeMs(state.Resume, state.PlayTarget) > 0,
                state.Providers,
                state.Provider);

            _seasons.Set(Load.Ready<IReadOnlyList<SeasonRef>>(show.Seasons));
            _cast.Set(Load.Ready(show.Cast));
            _recommendations.Set(Load.Ready<IReadOnlyList<MediaCard>>(show.Recommendations));
        }

        // Which season is showing lives outside the season cards' own list, so they are rebound
        // rather than resubmitted, and only when it moved: Render runs on every state change.
        if (seasonMoved) _seasons.Rebind();

        // Title and cards move together, off the season that arrived rather than the one merely
        // selected, so the previous episodes never sit under the next season's heading. Left
        // alone while the next season loads: an empty row drops HasItems, which SyncRows reads
        // as gone and pulls Cast up into its place.
        if (state.Season is Load<SeasonDetails>.Ready season)
        {
            _episodes.Retitle(
                Strings.Format(
                    RequireContext(),
                    ResourceConstant.String.rail_episodes_of_season,
                    season.Value.SeasonNumber));

            _episodes.Set(Load.Ready<IReadOnlyList<Episode>>(season.Value.Episodes));
        }
        else if (state.Season is Load<SeasonDetails>.Failed error)
        {
            _episodes.Set(Load.Failed<IReadOnlyList<Episode>>(error.Error));
        }

        SyncRows(
            (_seasons.HasItems, _seasons.Row),
            (_episodes.HasItems, _episodes.Row),
            (_cast.HasItems, _cast.Row),
            (_recommendations.HasItems, _recommendations.Row));

        FocusOpening(state);
    }

    // A page opened by a remote sender lands on what it named, once, after that season's cards
    // have arrived; later renders must not move focus out from under the user. The episode is
    // taken when the season has it, and the season card otherwise, so naming a season alone still
    // shows the viewer where they were sent. A season the show does not have moves nothing at
    // all, which is what naming no season does.
    private void FocusOpening(TvDetailState state)
    {
        if (!_focusPending || _seasons is null || _episodes is null) return;

        var seasons = state.Show.ValueOrDefault?.Seasons;

        if (seasons is not null && seasons.All(season => season.SeasonNumber != _openAtSeason))
        {
            _focusPending = false;
            return;
        }

        if (state.Season is not Load<SeasonDetails>.Ready loaded) return;
        if (loaded.Value.SeasonNumber != _openAtSeason) return;

        _focusPending = false;

        if (IndexOfEpisode(loaded.Value.Episodes) is { } episode)
        {
            FocusCard(_episodes.Row, episode);
            return;
        }

        if (IndexOfSeason(seasons) is { } season) FocusCard(_seasons.Row, season);
    }

    // Null when the season has no such episode, and when none was named.
    private int? IndexOfEpisode(IReadOnlyList<Episode> episodes)
    {
        for (var i = 0; i < episodes.Count; i++)
            if (episodes[i].EpisodeNumber == _openAtEpisode)
                return i;

        return null;
    }

    private int? IndexOfSeason(IReadOnlyList<SeasonRef>? seasons)
    {
        if (seasons is null) return null;

        for (var i = 0; i < seasons.Count; i++)
            if (seasons[i].SeasonNumber == _openAtSeason)
                return i;

        return null;
    }

    // An entry rolled forward after finishing an episode names one but holds no position, so it
    // says Play rather than Resume.
    private string? PlayLabel(WatchProgress? resume, TitleTarget? target)
    {
        var episode = resume is { SeasonNumber: { } season, EpisodeNumber: { } number }
            ? Episode.LabelFor(season, number)
            : null;

        return PlayLabel(ResumeMs(resume, target) > 0, episode, resume);
    }

    private static long ResumeMs(WatchProgress? resume, TitleTarget? target)
    {
        return target is { } aim ? resume?.ResumeMsFor(aim) ?? 0 : 0;
    }

    private ICharSequence Subtitle(TvShowDetails show)
    {
        return MetaText.HighlightedOver(
            RequireContext(),
            GenreLine(show.Genres),
            show.FirstAirYear?.ToString(),
            MetaText.Rating(show.Rating),
            show.Certification);
    }

    private void OnItemClicked(object? sender, BaseOnItemViewClickedEventArgs e)
    {
        switch (e.Item)
        {
            case var item when JavaRef.Unwrap<SeasonRef>(item) is { } season:
                _ = _viewModel?.SelectSeasonAsync(season.SeasonNumber);
                break;

            case var item when JavaRef.Unwrap<Episode>(item) is { } episode:
                var picked = new TitleTarget(MediaId, episode.SeasonNumber, episode.EpisodeNumber);

                Nav.OpenPlayback(
                    RequireContext(),
                    PlaybackArgs.For(
                        picked,
                        _viewModel?.State.Show.ValueOrDefault,
                        _viewModel?.State.Resume?.ResumeMsFor(picked) ?? 0,
                        _viewModel?.State.Provider));
                break;

            case var item when JavaRef.Unwrap<MediaCard>(item) is { } card:
                Nav.OpenDetails(RequireContext(), card.Id);
                break;
        }
    }
}
