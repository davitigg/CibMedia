using Android.Content;
using CibMedia.Core.Catalog;

namespace CibMedia.AndroidTv.App;

// Every Intent payload in the app: one screen writes these and another reads them back.
internal static class IntentExtras
{
    public const string TmdbId = "tmdbId";
    public const string Kind = "kind";
    public const string Season = "season";
    public const string Episode = "episode";
    public const string Title = "title";
    public const string PosterUrl = "posterUrl";
    public const string BackdropUrl = "backdropUrl";
    public const string Year = "year";
    public const string Rating = "rating";
    public const string ResumePositionMs = "resumePositionMs";
    public const string Provider = "provider";
    public const string LiveballUrl = "liveballUrl";

    public static void PutMediaId(Intent intent, MediaId id)
    {
        intent.PutExtra(TmdbId, id.TmdbId);
        intent.PutExtra(Kind, (int)id.Kind);
    }

    public static MediaId? GetMediaId(Intent? intent)
    {
        var tmdbId = intent?.GetIntExtra(TmdbId, -1) ?? -1;

        return tmdbId < 0 ? null : new MediaId(tmdbId, (MediaKind)intent!.GetIntExtra(Kind, 0));
    }

    public static int? GetOptionalInt(Intent? intent, string key)
    {
        return intent?.HasExtra(key) == true ? intent.GetIntExtra(key, 0) : null;
    }

    public static double? GetOptionalDouble(Intent? intent, string key)
    {
        return intent?.HasExtra(key) == true ? intent.GetDoubleExtra(key, 0) : null;
    }
}
