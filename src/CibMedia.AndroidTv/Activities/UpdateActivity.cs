using Android.Content.PM;
using CibMedia.AndroidTv.Leanback.Fragments;
using Fragment = AndroidX.Fragment.App.Fragment;

namespace CibMedia.AndroidTv.Activities;

[Activity(
    LaunchMode = LaunchMode.SingleTop,
    Theme = "@style/Theme.CibMedia.GuidedStep",
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.KeyboardHidden)]
public sealed class UpdateActivity : FragmentHostActivity
{
    protected override Fragment CreateFragment()
    {
        return new UpdateFragment();
    }
}
