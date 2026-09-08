namespace CibMedia.Core.Catalog;

public sealed record MediaCard(
    MediaId Id,
    string Title,
    string? PosterUrl,
    int? Year,
    double? Rating,
    string? BackdropUrl = null,
    int? SeasonNumber = null,
    int? EpisodeNumber = null);
