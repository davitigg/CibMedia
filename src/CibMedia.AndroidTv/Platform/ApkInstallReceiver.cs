using _Microsoft.Android.Resource.Designer;
using Android.Content;
using Android.Content.PM;
using CibMedia.AndroidTv.Design;

namespace CibMedia.AndroidTv.Platform;

// The session reports back here. Success never arrives: a successful install has already
// replaced the process this receiver lives in.
[BroadcastReceiver(Exported = false)]
public sealed class ApkInstallReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null || intent is null) return;

        var status = intent.GetIntExtra(PackageInstaller.ExtraStatus, int.MinValue);

        if (status == (int)PackageInstallStatus.PendingUserAction)
        {
            // The system asks for the confirmation itself; this only puts it on screen.
            if (Confirmation(intent) is { } confirm)
            {
                context.StartActivity(confirm.AddFlags(ActivityFlags.NewTask));
            }

            return;
        }

        if (status != (int)PackageInstallStatus.Success)
        {
            Toasts.Show(context, ResourceConstant.String.update_failed, Android.Widget.ToastLength.Long);
        }
    }

    private static Intent? Confirmation(Intent intent)
    {
        return OperatingSystem.IsAndroidVersionAtLeast(33)
            ? intent.GetParcelableExtra(Intent.ExtraIntent, Java.Lang.Class.FromType(typeof(Intent))) as Intent
#pragma warning disable CA1422
            : intent.GetParcelableExtra(Intent.ExtraIntent) as Intent;
#pragma warning restore CA1422
    }
}
