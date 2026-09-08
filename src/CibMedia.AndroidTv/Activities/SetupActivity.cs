using Android.Content.PM;
using CibMedia.AndroidTv.Leanback.Fragments;
using Fragment = AndroidX.Fragment.App.Fragment;

namespace CibMedia.AndroidTv.Activities;

// The setup screen reached from Settings; the first-run route hosts the same fragment inside
// MainActivity. SingleTop because this owns a listening port while it is in front, and a second
// instance would bind a second one.
[Activity(
    LaunchMode = LaunchMode.SingleTop,
    Theme = "@style/Theme.CibMedia.GuidedStep",
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.KeyboardHidden)]
public sealed class SetupActivity : FragmentHostActivity, IKeySetupHost
{
    public void OnKeysSaved()
    {
        Finish();
    }

    protected override Fragment CreateFragment()
    {
        return new ApiKeysSetupFragment();
    }
}
