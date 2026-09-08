namespace CibMedia.Core.Catalog;

public sealed record TvShowDetails(
    MediaId Id,
    string Title,
    string? Tagline,
    string? Overview,
    int? FirstAirYear,
    double? Rating,
    string? Certification,
    IReadOnlyList<GenreRef> Genres,
    string? PosterUrl,
    string? BackdropUrl,
    string? LogoUrl,
    IReadOnlyList<CastMember> Cast,
    IReadOnlyList<SeasonRef> Seasons,
    IReadOnlyList<MediaCard> Recommendations);
