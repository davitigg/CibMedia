namespace CibMedia.Core.Catalog;

public sealed record SeasonRef(
    int SeasonNumber,
    string Name,
    int EpisodeCount,
    int? Year,
    string? PosterUrl);
