using Android.Content.PM;
using CibMedia.AndroidTv.Leanback.Fragments;
using Fragment = AndroidX.Fragment.App.Fragment;

namespace CibMedia.AndroidTv.Activities;

[Activity(
    Theme = "@style/Theme.CibMedia",
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.KeyboardHidden)]
public sealed class SearchActivity : FragmentHostActivity
{
    protected override Fragment CreateFragment()
    {
        return new CatalogSearchFragment();
    }
}
