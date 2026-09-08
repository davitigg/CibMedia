using Android.Content;
using Android.Content.PM;
using CibMedia.Core.Catalog;
using CibMedia.AndroidTv.App;
using CibMedia.AndroidTv.Leanback.Fragments;
using Fragment = AndroidX.Fragment.App.Fragment;

namespace CibMedia.AndroidTv.Activities;

[Activity(
    Theme = "@style/Theme.CibMedia.Details",
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.KeyboardHidden)]
public sealed class DetailsActivity : FragmentHostActivity
{
    protected override Fragment CreateFragment()
    {
        return IntentExtras.GetMediaId(Intent)?.Kind == MediaKind.TvShow
            ? new TvDetailsFragment()
            : new MovieDetailsFragment();
    }

    // More Like This reuses this Activity. The Intent is swapped before the fragment is built,
    // because CreateFragment and the page both read it.
    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);

        if (intent is null) return;

        Intent = intent;
        ShowFragment();
    }
}
