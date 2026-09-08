using System.Collections.Concurrent;
using CibMedia.Core.Abstractions;
using CibMedia.Core.Catalog;
using CibMedia.Core.Common;
using CibMedia.Core.Playback;
using CibMedia.Core.Presentation.Details;
using CibMedia.Core.Presentation.Playback;

namespace CibMedia.Core.Presentation.TvDetail;

public sealed class TvDetailViewModel : DetailViewModel<TvDetailState>
{
    private readonly ICatalog _catalog;
    private readonly CancellationScope _playChecks;

    // Kept for as long as the page is open, so stepping back along the seasons rail never
    // passes through the loading state.
    private readonly ConcurrentDictionary<int, SeasonDetails> _seasons = new();
    private readonly CancellationScope _seasonLoads;
    private readonly IStreamProvider _streams;
    private readonly IPlaybackPreferences _preferences;

    private TitleTarget? _checkedTarget;

    public TvDetailViewModel(
        ICatalog catalog,
        IPlaybackHistory history,
        IStreamProvider streams,
        IPlaybackPreferences preferences,
        int tmdbId)
        : base(history, MediaId.TvShow(tmdbId), TvDetailState.Initial)
    {
        _catalog = catalog;
        _streams = streams;
        _preferences = preferences;
        _seasonLoads = new CancellationScope(Lifetime);
        _playChecks = new CancellationScope(Lifetime);
    }

    // openAt is a season named by a remote sender, and outranks watch history.
    public async Task LoadAsync(int? openAt = null)
    {
        var show = await Load.RunAsync(ct => _catalog.GetTvShowAsync(Id.TmdbId, ct), Lifetime)
            .ConfigureAwait(false);

        var seasons = show.ValueOrDefault?.Seasons;
        var resume = await ReadResumeAsync().ConfigureAwait(false);

        bool Exists(int? number)
        {
            return number is { } value && seasons?.Any(season => season.SeasonNumber == value) is true;
        }

        var opening = Exists(openAt)
            ? openAt!.Value
            : Exists(resume?.SeasonNumber)
                ? resume!.SeasonNumber!.Value
                : seasons is { Count: > 0 }
                    ? seasons[0].SeasonNumber
                    : 1;

        SetState(s => s with { Show = show, Resume = resume, SelectedSeason = opening });

        await SelectSeasonAsync(opening).ConfigureAwait(false);
    }

    // The selected season is decided only when the page opens: moving the rail out from under
    // someone because playback finished elsewhere is more surprising than useful.
    public override async Task ReloadResumeAsync()
    {
        var resume = await ReadResumeAsync().ConfigureAwait(false);

        SetState(s => s with { Resume = resume });

        await RefreshPlayAsync().ConfigureAwait(false);
    }

    public async Task SelectSeasonAsync(int seasonNumber)
    {
        var token = await _seasonLoads.NextAsync().ConfigureAwait(false);

        if (_seasons.TryGetValue(seasonNumber, out var loaded))
        {
            SetState(s => s with { SelectedSeason = seasonNumber, Season = Load.Ready(loaded) });

            await RefreshPlayAsync().ConfigureAwait(false);

            return;
        }

        SetState(s => s with { SelectedSeason = seasonNumber, Season = Load.Loading<SeasonDetails>() });

        try
        {
            var season = await Load.RunAsync(ct => _catalog.GetSeasonAsync(Id.TmdbId, seasonNumber, ct), token)
                .ConfigureAwait(false);

            if (token.IsCancellationRequested) return;

            if (season is Load<SeasonDetails>.Ready ready) _seasons[seasonNumber] = ready.Value;

            SetState(s => s with { Season = season });

            await RefreshPlayAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    protected override void OnDispose()
    {
        base.OnDispose();
        _seasonLoads.Dispose();
        _playChecks.Dispose();
    }

    // Play aims at the resume point, or the first episode of the season on show, so opening a
    // different season moves it and the availability check follows.
    private async Task RefreshPlayAsync()
    {
        var target = TargetFor(State);

        if (target is null || target == _checkedTarget) return;

        _checkedTarget = target;

        var token = await _playChecks.NextAsync().ConfigureAwait(false);

        SetState(s => s with { PlayTarget = target, Providers = Load.Loading<IReadOnlyList<string>>() });

        var providers = await PlayCheck.RunAsync(_streams, target, token).ConfigureAwait(false);

        if (token.IsCancellationRequested) return;

        // A provider chosen on this page outlives the episode it was chosen for.
        SetState(s => s with
        {
            Providers = providers,
            Provider = ProviderChoice.Preferred(providers.ValueOrDefault ?? [], s.Provider ?? _preferences.DefaultProvider)
        });
    }

    public void NextProvider()
    {
        SetState(s => s.Providers is Load<IReadOnlyList<string>>.Ready { Value.Count: > 1 } offered
            ? s with { Provider = ProviderChoice.Next(offered.Value, s.Provider) }
            : s);
    }

    private TitleTarget? TargetFor(TvDetailState state)
    {
        if (state.Show.ValueOrDefault is null) return null;

        if (state.Resume is { SeasonNumber: { } season, EpisodeNumber: { } episode })
            return new TitleTarget(Id, season, episode);

        var episodes = state.Season.ValueOrDefault?.Episodes;

        return new TitleTarget(
            Id,
            state.SelectedSeason,
            episodes is { Count: > 0 } list ? list[0].EpisodeNumber : 1);
    }
}
