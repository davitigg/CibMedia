using Android.Content;
using Android.Content.PM;
using CibMedia.Core.Abstractions;
using CibMedia.AndroidTv.App;
using CibMedia.AndroidTv.Leanback.Fragments;
using Microsoft.Extensions.DependencyInjection;
using Fragment = AndroidX.Fragment.App.Fragment;

namespace CibMedia.AndroidTv.Activities;

// Not MainLauncher: that adds the phone launcher category beside the leanback one, and a task
// created under one and reopened under the other is no match, so Android stacks a second shell.
[Activity(
    Label = "@string/app_name",
    Exported = true,
    LaunchMode = LaunchMode.SingleTop,
    Theme = "@style/Theme.CibMedia",
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.KeyboardHidden)]
[IntentFilter(
    [Intent.ActionMain],
    Categories = [Intent.CategoryLeanbackLauncher])]
public sealed class MainActivity : FragmentHostActivity, IKeySetupHost
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // The base finishes a duplicate shell before it builds anything to ask.
        if (IsFinishing) return;

        _ = OfferUpdateAsync();
    }

    // The manifest is read while the shell draws, so nothing here waits on it. A release worth
    // offering opens over what has already been drawn.
    private async Task OfferUpdateAsync()
    {
        if (!await AppServices.Provider.GetRequiredService<AppUpdateCheck>().RunOnceAsync()) return;

        if (IsFinishing || IsDestroyed) return;

        StartActivity(new Intent(this, typeof(UpdateActivity)));
    }

    // Swapped in place rather than stacked, so Back cannot peel setup off into an empty shell.
    public void OnKeysSaved()
    {
        ShowFragment();
    }

    protected override Fragment CreateFragment()
    {
        return AppServices.Provider.GetRequiredService<IApiKeys>().Complete()
            ? new ShellFragment()
            : new ApiKeysSetupFragment();
    }
}
