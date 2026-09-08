using CibMedia.Core.Abstractions;
using CibMedia.Core.Catalog;
using CibMedia.Core.Common;

namespace CibMedia.Core.Tests.Fakes;

// Generated pages and details. Each hook replaces one endpoint; the rest keep answering.
// Seasons 1-4 of ten episodes each.
public sealed class FakeCatalog(TimeSpan? delay = null) : ICatalog
{
    private static readonly string[] Titles =
    [
        "The Northern Line", "Glass Harbour", "Vanishing Point", "Ember & Ash",
        "Quiet Signal", "The Long Winter", "Salt Road", "Nightfall Protocol",
        "Paper Cities", "Iron Lantern", "Blue Meridian", "The Cartographer"
    ];

    private readonly TimeSpan _delay = delay ?? TimeSpan.Zero;

    public Func<int, CancellationToken, Task<Page<MediaCard>>>? Trending { get; set; }

    public List<string> Searches { get; } = [];

    public Func<int, int, CancellationToken, Task<SeasonDetails>>? Season { get; set; }

    public Func<int, CancellationToken, Task<MovieDetails>>? Movie { get; set; }

    public Func<int, CancellationToken, Task<TvShowDetails>>? Show { get; set; }

    public Task<Page<MediaCard>> GetTrendingAsync(int page, CancellationToken ct)
    {
        return Trending?.Invoke(page, ct) ?? Cards(MediaKind.Movie, page, 700, ct);
    }

    public Task<Page<MediaCard>> GetTrendingAsync(
        MediaKind kind,
        TrendingWindow window,
        int page,
        CancellationToken ct)
    {
        return Cards(kind, page, 700 + (window == TrendingWindow.Week ? 40 : 0), ct);
    }

    public Task<Page<MediaCard>> GetPopularMoviesAsync(int page, CancellationToken ct)
    {
        return Cards(MediaKind.Movie, page, 100, ct);
    }

    public Task<Page<MediaCard>> GetTopRatedMoviesAsync(int page, CancellationToken ct)
    {
        return Cards(MediaKind.Movie, page, 200, ct);
    }

    public Task<Page<MediaCard>> GetPopularTvShowsAsync(int page, CancellationToken ct)
    {
        return Cards(MediaKind.TvShow, page, 300, ct);
    }

    public Task<Page<MediaCard>> GetTopRatedTvShowsAsync(int page, CancellationToken ct)
    {
        return Cards(MediaKind.TvShow, page, 350, ct);
    }

    public Task<Page<MediaCard>> SearchAsync(string query, int page, CancellationToken ct)
    {
        Searches.Add(query);

        return string.IsNullOrWhiteSpace(query)
            ? Task.FromResult(Page<MediaCard>.Empty)
            : Cards(MediaKind.Movie, page, 400, ct);
    }

    public Task<Page<MediaCard>> DiscoverAsync(DiscoverFilter filter, int page, CancellationToken ct)
    {
        // Seeded off the genres so two rails on the same page hold different titles.
        var seed = (filter.GenreIdsAny ?? []).Concat(filter.GenreIdsAll ?? []).Sum();

        return Cards(filter.Kind, page, 500 + seed, ct);
    }

    public async Task<MovieDetails> GetMovieAsync(int tmdbId, CancellationToken ct)
    {
        if (Movie is not null) return await Movie(tmdbId, ct).ConfigureAwait(false);

        await Pause(ct).ConfigureAwait(false);

        return new MovieDetails(
            MediaId.Movie(tmdbId),
            Title(tmdbId),
            "Every road leads somewhere.",
            "A cartographer retracing a route her father never finished finds the map is "
            + "redrawing itself faster than she can follow it.",
            2024,
            131,
            8.4,
            "PG-13",
            [new GenreRef(1, "Action"), new GenreRef(4, "Sci-Fi")],
            null,
            null,
            null,
            [.. Enumerable.Range(1, 12).Select(i => new CastMember(i, $"Actor {i}", $"Character {i}", null))],
            (await Cards(MediaKind.Movie, 1, tmdbId + 11, ct).ConfigureAwait(false)).Items);
    }

    public async Task<TvShowDetails> GetTvShowAsync(int tmdbId, CancellationToken ct)
    {
        if (Show is not null) return await Show(tmdbId, ct).ConfigureAwait(false);

        await Pause(ct).ConfigureAwait(false);

        return new TvShowDetails(
            MediaId.TvShow(tmdbId),
            Title(tmdbId),
            "The signal was never noise.",
            "A coastal relay station picks up a broadcast that has not been transmitted yet.",
            2019,
            8.1,
            "TV-MA",
            [new GenreRef(3, "Drama"), new GenreRef(5, "Thriller")],
            null,
            null,
            null,
            [.. Enumerable.Range(1, 10).Select(i => new CastMember(i, $"Actor {i}", $"Character {i}", null))],
            [.. Enumerable.Range(1, 4).Select(i => new SeasonRef(i, $"Season {i}", 10, 2018 + i, null))],
            (await Cards(MediaKind.TvShow, 1, tmdbId + 13, ct).ConfigureAwait(false)).Items);
    }

    public Task<SeasonDetails> GetSeasonAsync(int tmdbId, int seasonNumber, CancellationToken ct)
    {
        return Season?.Invoke(tmdbId, seasonNumber, ct) ?? BuildSeasonAsync(seasonNumber, ct);
    }

    private static string Title(int id)
    {
        return Titles[Math.Abs(id) % Titles.Length];
    }

    private async Task<SeasonDetails> BuildSeasonAsync(int seasonNumber, CancellationToken ct)
    {
        await Pause(ct).ConfigureAwait(false);

        return new SeasonDetails(
            seasonNumber,
            $"Season {seasonNumber}",
            "The season where everything tilts.",
            [
                .. Enumerable.Range(1, 10).Select(i => new Episode(
                    seasonNumber,
                    i,
                    $"Episode {i}",
                    "Something happens, then something else.",
                    null,
                    38 + (i % 9),
                    Math.Round(7 + (i % 5 * 0.4), 1),
                    DateTimeOffset.UtcNow.AddDays(i - 7)))
            ]);
    }

    private async Task<Page<MediaCard>> Cards(MediaKind kind, int page, int seed, CancellationToken ct)
    {
        await Pause(ct).ConfigureAwait(false);

        var items = Enumerable.Range(0, 20)
            .Select(i =>
            {
                var id = seed + (page * 20) + i;

                return new MediaCard(
                    new MediaId(id, kind),
                    Title(id),
                    null,
                    1998 + (id % 28),
                    Math.Round(5.5 + (id % 9 * 0.5), 1));
            })
            .ToArray();

        return new Page<MediaCard>(items, page, 5);
    }

    private Task Pause(CancellationToken ct)
    {
        return _delay > TimeSpan.Zero ? Task.Delay(_delay, ct) : Task.CompletedTask;
    }
}
