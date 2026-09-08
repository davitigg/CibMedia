using CibMedia.Core.Catalog;
using CibMedia.Core.Common;
using CibMedia.Core.Playback;

namespace CibMedia.Core.Presentation.MovieDetail;

public sealed record MovieDetailState(
    Load<MovieDetails> Movie,
    WatchProgress? Resume = null,
    string? Provider = null)
{
    public static MovieDetailState Initial { get; } = new(Load.Loading<MovieDetails>());

    // Every provider the title has, once it has been asked about: Loading until then, Failed
    // with NotFound when it has nothing. The Provider action steps through them, and with one
    // it only says which.
    public Load<IReadOnlyList<string>> Providers { get; init; } = Load.Loading<IReadOnlyList<string>>();
}
