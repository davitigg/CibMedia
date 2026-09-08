namespace CibMedia.Core.Catalog;

public sealed record MovieDetails(
    MediaId Id,
    string Title,
    string? Tagline,
    string? Overview,
    int? Year,
    int? RuntimeMinutes,
    double? Rating,
    string? Certification,
    IReadOnlyList<GenreRef> Genres,
    string? PosterUrl,
    string? BackdropUrl,
    string? LogoUrl,
    IReadOnlyList<CastMember> Cast,
    IReadOnlyList<MediaCard> Recommendations)
{
    public string? RuntimeText => RuntimeMinutes is { } m and > 0 ? $"{m / 60}h {m % 60}m" : null;
}
