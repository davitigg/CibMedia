using _Microsoft.Android.Resource.Designer;
using AndroidX.Leanback.Widget;
using CibMedia.Core.Catalog;
using CibMedia.Core.Common;
using CibMedia.Core.Playback;
using CibMedia.Core.Presentation.MovieDetail;
using CibMedia.AndroidTv.App;
using CibMedia.AndroidTv.Design;
using CibMedia.AndroidTv.Leanback.Presenters;
using CibMedia.AndroidTv.Leanback.Support;
using CibMedia.AndroidTv.Playback;
using Java.Lang;
using Microsoft.Extensions.DependencyInjection;

namespace CibMedia.AndroidTv.Leanback.Fragments;

public sealed class MovieDetailsFragment : DetailsFragmentBase
{
    private StateBinding? _binding;
    private CardRow<CastMember>? _cast;
    private CardRow<MediaCard>? _recommendations;
    private MovieDetailViewModel? _viewModel;

    public override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        var presenters = AppServices.Provider.GetRequiredService<DetailPresenters>();

        _cast = new CardRow<CastMember>(
            1,
            GetString(ResourceConstant.String.rail_cast),
            presenters.Cast,
            member => member.Id);

        _recommendations = new CardRow<MediaCard>(
            2,
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

    protected override void StartLoading()
    {
        _viewModel = ActivatorUtilities.CreateInstance<MovieDetailViewModel>(
            AppServices.Provider,
            MediaId.TmdbId);

        _binding = StateBinding.Bind(this, _viewModel, Render);
        _ = _viewModel.LoadAsync();
    }

    protected override void OnPlay(bool fromStart)
    {
        var target = new TitleTarget(MediaId);

        Nav.OpenPlayback(
            RequireContext(),
            PlaybackArgs.For(
                target,
                _viewModel?.State.Movie.ValueOrDefault,
                fromStart ? 0 : ResumeMs(),
                _viewModel?.State.Provider));
    }

    protected override void OnNextProvider()
    {
        _viewModel?.NextProvider();
    }

    private long ResumeMs()
    {
        return _viewModel?.State.Resume?.ResumeMsFor(new TitleTarget(MediaId)) ?? 0;
    }

    private void Render()
    {
        if (_viewModel is null || _cast is null || _recommendations is null) return;

        if (_viewModel.State.Movie is Load<MovieDetails>.Failed failed)
        {
            BindError(failed.Error);
            return;
        }

        if (_viewModel.State.Movie is not Load<MovieDetails>.Ready ready) return;

        var movie = ready.Value;

        BindHeader(
            new Synopsis(movie.Title, Subtitle(movie), movie.Overview),
            movie.PosterUrl,
            movie.BackdropUrl,
            PlayLabel(ResumeMs() > 0, null, _viewModel.State.Resume),
            ResumeMs() > 0,
            _viewModel.State.Providers,
            _viewModel.State.Provider);

        _cast.Set(Load.Ready<IReadOnlyList<CastMember>>(movie.Cast));
        _recommendations.Set(Load.Ready(movie.Recommendations));

        SyncRows((_cast.HasItems, _cast.Row), (_recommendations.HasItems, _recommendations.Row));
    }

    private ICharSequence Subtitle(MovieDetails movie)
    {
        return MetaText.HighlightedOver(
            RequireContext(),
            GenreLine(movie.Genres),
            movie.Year?.ToString(),
            movie.RuntimeText,
            MetaText.Rating(movie.Rating),
            movie.Certification);
    }

    private void OnItemClicked(object? sender, BaseOnItemViewClickedEventArgs e)
    {
        if (JavaRef.Unwrap<MediaCard>(e.Item) is { } card) Nav.OpenDetails(RequireContext(), card.Id);
    }
}
