using CibMedia.Core.Abstractions;
using CibMedia.Core.Catalog;
using CibMedia.Core.Common;
using TMDbLib.Client;
using TMDbLib.Objects.Discover;
using TMDbLib.Objects.Movies;
using TMDbLib.Objects.Search;
using TMDbLib.Objects.Trending;
using TMDbLib.Objects.TvShows;

namespace CibMedia.Core.Infrastructure.Tmdb;

// Every call ensures the image configuration first: TMDb returns bare artwork paths and the
// client cannot compose a URL until it holds one. The call is a no-op after the first.
public sealed class TmdbCatalog(TmdbClientSource clients, TmdbArtwork artwork) : ICatalog
{
    private TMDbClient Client => clients.Client;

    // Without a vote floor, sorting by rating surfaces titles carrying a single 10/10.
    private const int RatingSortMinVoteCount = 200;

    public async Task<Page<MediaCard>> GetTrendingAsync(int page, CancellationToken ct)
    {
        await artwork.EnsureConfigAsync(ct).ConfigureAwait(false);

        return (await Client.GetTrendingAllAsync(TimeWindow.Day, page, cancellationToken: ct).ConfigureAwait(false))
            .ToPage(artwork);
    }

    public async Task<Page<MediaCard>> GetTrendingAsync(
        MediaKind kind,
        TrendingWindow window,
        int page,
        CancellationToken ct)
    {
        await artwork.EnsureConfigAsync(ct).ConfigureAwait(false);

        var when = window == TrendingWindow.Week ? TimeWindow.Week : TimeWindow.Day;

        return kind == MediaKind.Movie
            ? (await Client.GetTrendingMoviesAsync(when, page, cancellationToken: ct).ConfigureAwait(false))
                .ToPage(artwork)
            : (await Client.GetTrendingTvAsync(when, page, cancellationToken: ct).ConfigureAwait(false))
                .ToPage(artwork);
    }

    public async Task<Page<MediaCard>> GetPopularMoviesAsync(int page, CancellationToken ct)
    {
        await artwork.EnsureConfigAsync(ct).ConfigureAwait(false);

        return (await Client.GetMoviePopularListAsync(page: page, cancellationToken: ct).ConfigureAwait(false))
            .ToPage(artwork);
    }

    public async Task<Page<MediaCard>> GetTopRatedMoviesAsync(int page, CancellationToken ct)
    {
        await artwork.EnsureConfigAsync(ct).ConfigureAwait(false);

        return (await Client.GetMovieTopRatedListAsync(page: page, cancellationToken: ct).ConfigureAwait(false))
            .ToPage(artwork);
    }

    public async Task<Page<MediaCard>> GetPopularTvShowsAsync(int page, CancellationToken ct)
    {
        await artwork.EnsureConfigAsync(ct).ConfigureAwait(false);

        return (await Client.GetTvShowPopularAsync(page, cancellationToken: ct).ConfigureAwait(false))
            .ToPage(artwork);
    }

    public async Task<Page<MediaCard>> GetTopRatedTvShowsAsync(int page, CancellationToken ct)
    {
        await artwork.EnsureConfigAsync(ct).ConfigureAwait(false);

        return (await Client.GetTvShowTopRatedAsync(page, cancellationToken: ct).ConfigureAwait(false))
            .ToPage(artwork);
    }

    public async Task<Page<MediaCard>> SearchAsync(string query, int page, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query)) return Page<MediaCard>.Empty;

        await artwork.EnsureConfigAsync(ct).ConfigureAwait(false);

        return (await Client.SearchMultiAsync(query, page, cancellationToken: ct).ConfigureAwait(false))
            .ToPage(artwork);
    }

    public async Task<Page<MediaCard>> DiscoverAsync(DiscoverFilter filter, int page, CancellationToken ct)
    {
        await artwork.EnsureConfigAsync(ct).ConfigureAwait(false);

        return filter.Kind == MediaKind.Movie
            ? (await DiscoverMovies(filter).Query(page, ct).ConfigureAwait(false)).ToPage(artwork)
            : (await DiscoverShows(filter).Query(page, ct).ConfigureAwait(false)).ToPage(artwork);
    }

    public async Task<MovieDetails> GetMovieAsync(int tmdbId, CancellationToken ct)
    {
        await artwork.EnsureConfigAsync(ct).ConfigureAwait(false);

        var movie = await Client.GetMovieAsync(
                tmdbId,
                MovieMethods.Images |
                MovieMethods.Credits |
                MovieMethods.Recommendations |
                MovieMethods.ReleaseDates |
                MovieMethods.ExternalIds,
                ct)
            .ConfigureAwait(false);

        return (movie ?? throw Missing($"movie {tmdbId}")).ToDetails(artwork);
    }

    public async Task<TvShowDetails> GetTvShowAsync(int tmdbId, CancellationToken ct)
    {
        await artwork.EnsureConfigAsync(ct).ConfigureAwait(false);

        var show = await Client.GetTvShowAsync(
                tmdbId,
                TvShowMethods.Images |
                TvShowMethods.CreditsAggregate |
                TvShowMethods.Recommendations |
                TvShowMethods.ContentRatings |
                TvShowMethods.ExternalIds,
                Client.DefaultLanguage,
                Client.DefaultImageLanguage,
                ct)
            .ConfigureAwait(false);

        return (show ?? throw Missing($"show {tmdbId}")).ToDetails(artwork);
    }

    public async Task<SeasonDetails> GetSeasonAsync(int tmdbId, int seasonNumber, CancellationToken ct)
    {
        await artwork.EnsureConfigAsync(ct).ConfigureAwait(false);

        var season = await Client.GetTvSeasonAsync(
                tmdbId,
                seasonNumber,
                language: Client.DefaultLanguage,
                includeImageLanguage: Client.DefaultImageLanguage,
                cancellationToken: ct)
            .ConfigureAwait(false);

        return (season ?? throw Missing($"season {seasonNumber} of show {tmdbId}")).ToDetails(artwork);
    }

    // TMDbLib answers a 404 with null rather than an exception, so the kind is set here or a
    // caller cannot tell an id that names nothing from a TMDb that could not be reached.
    private static UpstreamException Missing(string what)
    {
        return new UpstreamException(AppErrorKind.NotFound, $"TMDb has no {what}.");
    }

    private DiscoverMovie DiscoverMovies(DiscoverFilter filter)
    {
        var sort = MovieSort(filter.Sort);
        var query = Client.DiscoverMoviesAsync().OrderBy(sort);

        if (sort is DiscoverMovieSortBy.VoteAverage or DiscoverMovieSortBy.VoteAverageDesc)
            query = query.WhereVoteCountIsAtLeast(RatingSortMinVoteCount);

        // with_genres holds one mode at a time; All is applied last so it wins.
        if (filter.GenreIdsAny is { Count: > 0 } any) query = query.IncludeWithAnyOfGenre(any);
        if (filter.GenreIdsAll is { Count: > 0 } all) query = query.IncludeWithAllOfGenre(all);

        return query;
    }

    private DiscoverTv DiscoverShows(DiscoverFilter filter)
    {
        var sort = TvSort(filter.Sort);
        var discover = new AnyGenreDiscoverTv(Client);

        if (filter.GenreIdsAny is { Count: > 0 } any) discover.IncludeWithAnyOfGenre(any);

        var query = discover.OrderBy(sort);

        if (sort is DiscoverTvShowSortBy.VoteAverage or DiscoverTvShowSortBy.VoteAverageDesc)
            query = query.WhereVoteCountIsAtLeast(RatingSortMinVoteCount);

        if (filter.GenreIdsAll is { Count: > 0 } all) query = query.WhereGenresInclude(all);

        return query;
    }

    private static DiscoverMovieSortBy MovieSort(SortOrder sort)
    {
        return sort switch
        {
            SortOrder.Rating => DiscoverMovieSortBy.VoteAverageDesc,
            SortOrder.Newest => DiscoverMovieSortBy.PrimaryReleaseDateDesc,
            _ => DiscoverMovieSortBy.PopularityDesc
        };
    }

    private static DiscoverTvShowSortBy TvSort(SortOrder sort)
    {
        return sort switch
        {
            SortOrder.Rating => DiscoverTvShowSortBy.VoteAverageDesc,
            SortOrder.Newest => DiscoverTvShowSortBy.FirstAirDateDesc,
            _ => DiscoverTvShowSortBy.PopularityDesc
        };
    }
}
