using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using CibMedia.Core.Abstractions;
using CibMedia.Core.Catalog;
using CibMedia.Core.Common;

namespace CibMedia.Core.Infrastructure;

// Per-endpoint TTLs set to how often each answer actually changes. Entries are the domain
// records themselves, so a record that gains a member reads back out of an older entry with
// that member defaulted; only the TTL and Settings' Clear cache retire them.
public sealed class CachedCatalog(ICatalog inner, ICacheStore store) : ICatalog
{
    // TMDb recomputes trending once a day.
    private static readonly TimeSpan Trending = TimeSpan.FromHours(6);

    private static readonly TimeSpan Lists = TimeSpan.FromHours(12);

    // A show gains episodes while it airs, so this has to stay inside a broadcast week.
    private static readonly TimeSpan Series = TimeSpan.FromHours(6);

    private static readonly TimeSpan Movie = TimeSpan.FromDays(3);

    public Task<Page<MediaCard>> GetTrendingAsync(int page, CancellationToken ct)
    {
        return CardsAsync($"trending:{page}", Trending, c => inner.GetTrendingAsync(page, c), ct);
    }

    public Task<Page<MediaCard>> GetTrendingAsync(
        MediaKind kind,
        TrendingWindow window,
        int page,
        CancellationToken ct)
    {
        return CardsAsync(
            $"trending:{kind}:{window}:{page}",
            Trending,
            c => inner.GetTrendingAsync(kind, window, page, c),
            ct);
    }

    public Task<Page<MediaCard>> GetPopularMoviesAsync(int page, CancellationToken ct)
    {
        return CardsAsync($"movies:popular:{page}", Lists, c => inner.GetPopularMoviesAsync(page, c), ct);
    }

    public Task<Page<MediaCard>> GetTopRatedMoviesAsync(int page, CancellationToken ct)
    {
        return CardsAsync($"movies:top:{page}", Lists, c => inner.GetTopRatedMoviesAsync(page, c), ct);
    }

    public Task<Page<MediaCard>> GetPopularTvShowsAsync(int page, CancellationToken ct)
    {
        return CardsAsync($"tv:popular:{page}", Lists, c => inner.GetPopularTvShowsAsync(page, c), ct);
    }

    public Task<Page<MediaCard>> GetTopRatedTvShowsAsync(int page, CancellationToken ct)
    {
        return CardsAsync($"tv:top:{page}", Lists, c => inner.GetTopRatedTvShowsAsync(page, c), ct);
    }

    // Uncached: a query is typed once and its answer is stale immediately.
    public Task<Page<MediaCard>> SearchAsync(string query, int page, CancellationToken ct)
    {
        return inner.SearchAsync(query, page, ct);
    }

    public Task<Page<MediaCard>> DiscoverAsync(DiscoverFilter filter, int page, CancellationToken ct)
    {
        return CardsAsync(
            $"discover:{filter.Kind}:any={Ids(filter.GenreIdsAny)}:all={Ids(filter.GenreIdsAll)}:{filter.Sort}:{page}",
            Lists,
            c => inner.DiscoverAsync(filter, page, c),
            ct);
    }

    public Task<MovieDetails> GetMovieAsync(int tmdbId, CancellationToken ct)
    {
        return ThroughCacheAsync(
            $"movie:{tmdbId}",
            Movie,
            CacheJson.Default.MovieDetails,
            c => inner.GetMovieAsync(tmdbId, c),
            ct);
    }

    public Task<TvShowDetails> GetTvShowAsync(int tmdbId, CancellationToken ct)
    {
        return ThroughCacheAsync(
            $"tv:{tmdbId}",
            Series,
            CacheJson.Default.TvShowDetails,
            c => inner.GetTvShowAsync(tmdbId, c),
            ct);
    }

    public Task<SeasonDetails> GetSeasonAsync(int tmdbId, int seasonNumber, CancellationToken ct)
    {
        return ThroughCacheAsync(
            $"tv:{tmdbId}:season:{seasonNumber}",
            Series,
            CacheJson.Default.SeasonDetails,
            c => inner.GetSeasonAsync(tmdbId, seasonNumber, c),
            ct);
    }

    private static string Ids(IReadOnlyList<int>? ids)
    {
        return ids is { Count: > 0 } ? string.Join('+', ids) : string.Empty;
    }

    private Task<Page<MediaCard>> CardsAsync(
        string key,
        TimeSpan maxAge,
        Func<CancellationToken, Task<Page<MediaCard>>> fetch,
        CancellationToken ct)
    {
        return ThroughCacheAsync(key, maxAge, CacheJson.Default.PageMediaCard, fetch, ct);
    }

    private async Task<T> ThroughCacheAsync<T>(
        string key,
        TimeSpan maxAge,
        JsonTypeInfo<T> typeInfo,
        Func<CancellationToken, Task<T>> fetch,
        CancellationToken ct)
    {
        try
        {
            if (await store.ReadAsync(key, maxAge, ct).ConfigureAwait(false) is { } cached
                && JsonSerializer.Deserialize(cached, typeInfo) is { } value)
                return value;
        }
        catch (JsonException)
        {
        }

        var fresh = await fetch(ct).ConfigureAwait(false);

        try
        {
            await store.WriteAsync(key, JsonSerializer.SerializeToUtf8Bytes(fresh, typeInfo), ct)
                .ConfigureAwait(false);
        }
        catch (IOException)
        {
        }

        return fresh;
    }
}
