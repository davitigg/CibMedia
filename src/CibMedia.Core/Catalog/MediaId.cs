namespace CibMedia.Core.Catalog;

public readonly record struct MediaId(int TmdbId, MediaKind Kind)
{
    public static MediaId Movie(int tmdbId)
    {
        return new MediaId(tmdbId, MediaKind.Movie);
    }

    public static MediaId TvShow(int tmdbId)
    {
        return new MediaId(tmdbId, MediaKind.TvShow);
    }

    public override string ToString()
    {
        return $"{Kind}/{TmdbId}";
    }
}
