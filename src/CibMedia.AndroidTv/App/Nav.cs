using Android.Content;
using CibMedia.Core.Catalog;
using CibMedia.AndroidTv.Activities;
using CibMedia.AndroidTv.Playback;

namespace CibMedia.AndroidTv.App;

// Navigation is Activities and Intents, so Back and task history are the platform's job.
public static class Nav
{
    // One details page for the whole task: More Like This opens details from details, and
    // ClearTop with SingleTop hands the running instance a new Intent instead of stacking,
    // so Back is always one press from browsing. Season and episode are where the page opens,
    // not what it plays.
    public static void OpenDetails(Context context, MediaId id, int? season = null, int? episode = null)
    {
        var intent = new Intent(context, typeof(DetailsActivity));
        intent.SetFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop);
        IntentExtras.PutMediaId(intent, id);

        if (season is { } number) intent.PutExtra(IntentExtras.Season, number);
        if (episode is { } which) intent.PutExtra(IntentExtras.Episode, which);

        context.StartActivity(intent);
    }

    public static void OpenSearch(Context context)
    {
        context.StartActivity(new Intent(context, typeof(SearchActivity)));
    }

    public static void OpenPlayback(Context context, PlaybackArgs args)
    {
        var intent = new Intent(context, typeof(PlaybackActivity));
        args.WriteTo(intent);

        context.StartActivity(intent);
    }
}
