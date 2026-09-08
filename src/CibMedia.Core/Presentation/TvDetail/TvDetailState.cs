using CibMedia.Core.Catalog;
using CibMedia.Core.Common;
using CibMedia.Core.Playback;

namespace CibMedia.Core.Presentation.TvDetail;

public sealed record TvDetailState(
    Load<TvShowDetails> Show,
    int SelectedSeason,
    Load<SeasonDetails> Season,
    WatchProgress? Resume = null,
    TitleTarget? PlayTarget = null,
    string? Provider = null)
{
    // Every provider the play target has, once it has been asked about: Loading until then,
    // Failed with NotFound when it has nothing. The Provider action steps through them, and with
    // one it only says which.
    public Load<IReadOnlyList<string>> Providers { get; init; } = Load.Loading<IReadOnlyList<string>>();

    public static TvDetailState Initial { get; } = new(
        Load.Loading<TvShowDetails>(),
        1,
        Load.Loading<SeasonDetails>());
}
