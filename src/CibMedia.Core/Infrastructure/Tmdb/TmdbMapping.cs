using CibMedia.Core.Catalog;
using CibMedia.Core.Common;
using TMDbLib.Objects.General;
using TMDbLib.Objects.Search;
using Movie = TMDbLib.Objects.Movies.Movie;
using MovieCredits = TMDbLib.Objects.Movies.Credits;
using TvShow = TMDbLib.Objects.TvShows.TvShow;
using TvSeason = TMDbLib.Objects.TvShows.TvSeason;
using TvCredits = TMDbLib.Objects.TvShows.Credits;
using TvCreditsAggregate = TMDbLib.Objects.TvShows.CreditsAggregate;

namespace CibMedia.Core.Infrastructure.Tmdb;

// The only place that knows TMDb's field names.
internal static class TmdbMapping
{
    private const int MaxCast = 20;

    // Multi-search and all-trending are mixed arrays. People are dropped; TMDb has no
    // parameter to exclude them, so the paging counts stay as reported.
    public static Page<MediaCard> ToPage(this SearchContainer<SearchBase> container, TmdbArtwork artwork)
    {
        return new Page<MediaCard>(
            [
                .. (container.Results ?? [])
                    .Select(row => row switch
                    {
                        SearchMovie movie => movie.ToCard(artwork),
                        SearchTv show => show.ToCard(artwork),
                        _ => (MediaCard?)null
                    })
                    .OfType<MediaCard>()
            ],
            container.Page,
            container.TotalPages);
    }

    public static Page<MediaCard> ToPage(this SearchContainer<SearchMovie> container, TmdbArtwork artwork)
    {
        return new Page<MediaCard>(
            [.. (container.Results ?? []).Select(m => m.ToCard(artwork))],
            container.Page,
            container.TotalPages);
    }

    public static Page<MediaCard> ToPage(this SearchContainer<SearchTv> container, TmdbArtwork artwork)
    {
        return new Page<MediaCard>(
            [.. (container.Results ?? []).Select(t => t.ToCard(artwork))],
            container.Page,
            container.TotalPages);
    }

    public static MediaCard ToCard(this SearchMovie movie, TmdbArtwork artwork)
    {
        return new MediaCard(
            MediaId.Movie(movie.Id),
            movie.Title ?? movie.OriginalTitle ?? string.Empty,
            artwork.Poster(movie.PosterPath),
            movie.ReleaseDate?.Year,
            Rating(movie.VoteAverage),
            artwork.Backdrop(movie.BackdropPath));
    }

    public static MediaCard ToCard(this SearchTv show, TmdbArtwork artwork)
    {
        return new MediaCard(
            MediaId.TvShow(show.Id),
            show.Name ?? show.OriginalName ?? string.Empty,
            artwork.Poster(show.PosterPath),
            show.FirstAirDate?.Year,
            Rating(show.VoteAverage),
            artwork.Backdrop(show.BackdropPath));
    }

    public static MovieDetails ToDetails(this Movie movie, TmdbArtwork artwork)
    {
        return new MovieDetails(
            MediaId.Movie(movie.Id),
            movie.Title ?? movie.OriginalTitle ?? string.Empty,
            Blank(movie.Tagline),
            Blank(movie.Overview),
            movie.ReleaseDate?.Year,
            movie.Runtime,
            Rating(movie.VoteAverage),
            Certification(movie),
            [.. (movie.Genres ?? []).Select(g => new GenreRef(g.Id, g.Name ?? string.Empty))],
            artwork.Poster(movie.PosterPath),
            artwork.Backdrop(movie.BackdropPath),
            artwork.Logo(PreferredLogo(movie.Images)),
            Cast(movie.Credits, artwork),
            [.. (movie.Recommendations?.Results ?? []).Select(m => m.ToCard(artwork))]);
    }

    public static TvShowDetails ToDetails(this TvShow show, TmdbArtwork artwork)
    {
        return new TvShowDetails(
            MediaId.TvShow(show.Id),
            show.Name ?? show.OriginalName ?? string.Empty,
            Blank(show.Tagline),
            Blank(show.Overview),
            show.FirstAirDate?.Year,
            Rating(show.VoteAverage),
            Certification(show),
            [.. (show.Genres ?? []).Select(g => new GenreRef(g.Id, g.Name ?? string.Empty))],
            artwork.Poster(show.PosterPath),
            artwork.Backdrop(show.BackdropPath),
            artwork.Logo(PreferredLogo(show.Images)),
            Cast(show.AggregateCredits, show.Credits, artwork),
            // Season 0 is TMDb's "Specials" bucket.
            [
                .. (show.Seasons ?? [])
                .Where(s => s.SeasonNumber > 0)
                .OrderBy(s => s.SeasonNumber)
                .Select(s => new SeasonRef(
                    s.SeasonNumber,
                    s.Name ?? $"Season {s.SeasonNumber}",
                    s.EpisodeCount,
                    s.AirDate?.Year,
                    artwork.Poster(s.PosterPath)))
            ],
            [.. (show.Recommendations?.Results ?? []).Select(t => t.ToCard(artwork))]);
    }

    public static SeasonDetails ToDetails(this TvSeason season, TmdbArtwork artwork)
    {
        return new SeasonDetails(
            season.SeasonNumber,
            season.Name ?? $"Season {season.SeasonNumber}",
            Blank(season.Overview),
            [
                .. (season.Episodes ?? []).Select(e => new Episode(
                    e.SeasonNumber,
                    (int)e.EpisodeNumber,
                    e.Name ?? $"Episode {e.EpisodeNumber}",
                    Blank(e.Overview),
                    artwork.Still(e.StillPath),
                    e.Runtime,
                    Rating(e.VoteAverage),
                    AirDate(e.AirDate)))
            ]);
    }

    // A show's cast lives in aggregateCredits, where a role spans episodes and carries roles[]
    // rather than one character; TMDb leaves plain credits null for most shows.
    private static IReadOnlyList<CastMember> Cast(
        TvCreditsAggregate? aggregate,
        TvCredits? credits,
        TmdbArtwork artwork)
    {
        var cast = aggregate?.Cast;

        return cast is null or { Count: 0 }
            ? Cast(credits, artwork)
            :
            [
                .. cast
                    .OrderBy(c => c.Order)
                    .Take(MaxCast)
                    .Select(c => new CastMember(
                        c.Id,
                        c.Name ?? string.Empty,
                        Role(c.Roles?.FirstOrDefault()?.Character, c.Name),
                        artwork.Profile(c.ProfilePath)))
            ];
    }

    private static IReadOnlyList<CastMember> Cast(TvCredits? credits, TmdbArtwork artwork)
    {
        return
        [
            .. (credits?.Cast ?? [])
            .OrderBy(c => c.Order)
            .Take(MaxCast)
            .Select(c => new CastMember(
                c.Id,
                c.Name ?? string.Empty,
                Role(c.Character, c.Name),
                artwork.Profile(c.ProfilePath)))
        ];
    }

    private static IReadOnlyList<CastMember> Cast(MovieCredits? credits, TmdbArtwork artwork)
    {
        return
        [
            .. (credits?.Cast ?? [])
            .OrderBy(c => c.Order)
            .Take(MaxCast)
            .Select(c => new CastMember(
                c.Id,
                c.Name ?? string.Empty,
                Role(c.Character, c.Name),
                artwork.Profile(c.ProfilePath)))
        ];
    }

    // TMDb returns every country's certificate; US is the only market populated for most.
    private static string? Certification(Movie movie)
    {
        return movie.ReleaseDates?.Results?
            .FirstOrDefault(r => r.Iso_3166_1 == "US")?
            .ReleaseDates?
            .Select(d => d.Certification)
            .FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));
    }

    private static string? Certification(TvShow show)
    {
        return show.ContentRatings?.Results?
            .FirstOrDefault(r => r.Iso_3166_1 == "US")?
            .Rating is { Length: > 0 } rating
            ? rating
            : null;
    }

    // English first, then language-neutral; TMDb often carries a dozen.
    private static string? PreferredLogo(Images? images)
    {
        var logos = images?.Logos;
        if (logos is null || logos.Count == 0) return null;

        return (logos.FirstOrDefault(l => l.Iso_639_1 == "en")
                ?? logos.FirstOrDefault(l => string.IsNullOrEmpty(l.Iso_639_1))
                ?? logos[0]).FilePath;
    }

    // TMDb air dates are calendar days and TMDbLib hands them back with an unspecified Kind;
    // left alone, DateTimeOffset would stamp them with the box's local offset.
    private static DateTimeOffset? AirDate(DateTime? date)
    {
        return date is { } value ? new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)) : null;
    }

    private static string? Blank(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    // TMDb sometimes fills the character with the actor's own name.
    private static string? Role(string? character, string? name)
    {
        return Blank(character) is { } role && !string.Equals(role, name, StringComparison.OrdinalIgnoreCase)
            ? role
            : null;
    }

    // TMDb writes 0 for "nobody has voted".
    private static double? Rating(double value)
    {
        return value > 0 ? Math.Round(value, 1) : null;
    }
}
