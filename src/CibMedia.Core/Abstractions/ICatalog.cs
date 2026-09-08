using CibMedia.Core.Catalog;
using CibMedia.Core.Common;

namespace CibMedia.Core.Abstractions;

public interface ICatalog
{
    // Movies and shows mixed in one list; the per-kind overload is for a catalogue section.
    Task<Page<MediaCard>> GetTrendingAsync(int page, CancellationToken ct);

    Task<Page<MediaCard>> GetTrendingAsync(MediaKind kind, TrendingWindow window, int page, CancellationToken ct);

    Task<Page<MediaCard>> GetPopularMoviesAsync(int page, CancellationToken ct);

    Task<Page<MediaCard>> GetTopRatedMoviesAsync(int page, CancellationToken ct);

    Task<Page<MediaCard>> GetPopularTvShowsAsync(int page, CancellationToken ct);

    Task<Page<MediaCard>> GetTopRatedTvShowsAsync(int page, CancellationToken ct);

    Task<Page<MediaCard>> SearchAsync(string query, int page, CancellationToken ct);

    Task<Page<MediaCard>> DiscoverAsync(DiscoverFilter filter, int page, CancellationToken ct);

    Task<MovieDetails> GetMovieAsync(int tmdbId, CancellationToken ct);

    Task<TvShowDetails> GetTvShowAsync(int tmdbId, CancellationToken ct);

    Task<SeasonDetails> GetSeasonAsync(int tmdbId, int seasonNumber, CancellationToken ct);
}
