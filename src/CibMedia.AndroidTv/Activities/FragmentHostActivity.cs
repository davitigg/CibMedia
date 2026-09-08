using _Microsoft.Android.Resource.Designer;
using Android.Content;
using Android.Views;
using AndroidX.Fragment.App;
using CibMedia.AndroidTv.App;
using CibMedia.AndroidTv.Design;
using Fragment = AndroidX.Fragment.App.Fragment;

namespace CibMedia.AndroidTv.Activities;

// Every screen is a Leanback fragment and all its Activity does is host it, so Back, the task
// stack and focus restoration come from the platform.
public abstract class FragmentHostActivity : FragmentActivity
{
    protected abstract Fragment CreateFragment();

    protected override void AttachBaseContext(Context? @base)
    {
        base.AttachBaseContext(ScaledContext.Wrap(@base));
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Android resumes an existing task only when the launcher intent equals the one that
        // created it; a mismatch stacks a second shell. This catches a task already in that state.
        if (!IsTaskRoot && IsLauncherIntent(Intent))
        {
            Finish();
            return;
        }

        AppServices.Initialise(this);

        var host = new FrameLayout(this) { Id = ResourceConstant.Id.content_host };
        host.SetBackgroundColor(Tokens.BgBase(this));
        SetContentView(
            host,
            new ViewGroup.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.MatchParent));

        // After a recreation the FragmentManager has already restored the previous instance.
        if (savedInstanceState is null) ShowFragment();
    }

    private static bool IsLauncherIntent(Intent? intent)
    {
        return intent?.Action == Intent.ActionMain
               && (intent.HasCategory(Intent.CategoryLeanbackLauncher)
                   || intent.HasCategory(Intent.CategoryLauncher));
    }

    // For an Activity that is reused rather than stacked: a new Intent means a new screen.
    protected void ShowFragment()
    {
        SupportFragmentManager.BeginTransaction()
            .Replace(ResourceConstant.Id.content_host, CreateFragment())
            .Commit();
    }
}
