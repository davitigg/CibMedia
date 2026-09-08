namespace CibMedia.Core.Catalog;

public sealed record SeasonDetails(
    int SeasonNumber,
    string Name,
    string? Overview,
    IReadOnlyList<Episode> Episodes);
