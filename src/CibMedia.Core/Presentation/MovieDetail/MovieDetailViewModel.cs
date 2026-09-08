using CibMedia.Core.Abstractions;
using CibMedia.Core.Catalog;
using CibMedia.Core.Common;
using CibMedia.Core.Playback;
using CibMedia.Core.Presentation.Details;
using CibMedia.Core.Presentation.Playback;

namespace CibMedia.Core.Presentation.MovieDetail;

public sealed class MovieDetailViewModel(
    ICatalog catalog,
    IPlaybackHistory history,
    IStreamProvider streams,
    IPlaybackPreferences preferences,
    int tmdbId)
    : DetailViewModel<MovieDetailState>(history, MediaId.Movie(tmdbId), MovieDetailState.Initial)
{
    // The details win the race to the screen; the availability check follows so the page can
    // say a title has nothing to play before the user has finished reading about it.
    public async Task LoadAsync()
    {
        var check = PlayCheck.RunAsync(streams, new TitleTarget(Id), Lifetime);
        var result = await Load.RunAsync(ct => catalog.GetMovieAsync(Id.TmdbId, ct), Lifetime)
            .ConfigureAwait(false);
        var resume = await ReadResumeAsync().ConfigureAwait(false);

        SetState(s => s with { Movie = result, Resume = resume });

        var providers = await check.ConfigureAwait(false);

        SetState(s => s with
        {
            Providers = providers,
            Provider = ProviderChoice.Preferred(providers.ValueOrDefault ?? [], preferences.DefaultProvider)
        });
    }

    public void NextProvider()
    {
        SetState(s => s.Providers is Load<IReadOnlyList<string>>.Ready { Value.Count: > 1 } offered
            ? s with { Provider = ProviderChoice.Next(offered.Value, s.Provider) }
            : s);
    }

    public override async Task ReloadResumeAsync()
    {
        var resume = await ReadResumeAsync().ConfigureAwait(false);

        SetState(s => s with { Resume = resume });
    }
}
