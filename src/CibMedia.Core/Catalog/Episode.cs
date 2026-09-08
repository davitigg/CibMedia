namespace CibMedia.Core.Catalog;

public sealed record Episode(
    int SeasonNumber,
    int EpisodeNumber,
    string Title,
    string? Overview,
    string? StillUrl,
    int? RuntimeMinutes,
    double? Rating,
    DateTimeOffset? AirDate)
{
    public string Label => LabelFor(SeasonNumber, EpisodeNumber);

    public static string LabelFor(int season, int episode)
    {
        return $"S{season}E{episode}";
    }

    public string? RuntimeText => RuntimeMinutes is { } m and > 0 ? $"{m}m" : null;

    // TMDb has no date for an unaired episode. Compared as UTC dates rather than instants:
    // the value is a midnight-UTC date, so an episode airing today would otherwise read as
    // unaired for most of that day.
    public bool HasAired =>
        AirDate is { } date && date.UtcDateTime.Date <= DateTime.UtcNow.Date;
}
