using Android.Content;
using Android.Content.PM;
using Android.Provider;

namespace CibMedia.AndroidTv.Platform;

// Committing a session replaces this app, so nothing survives the call: whatever should happen
// after an update happens on the next launch.
internal static class ApkInstaller
{
    private const string SessionName = "apk";

    // Below Oreo there is no per-app grant to hold: unknown sources is one switch for the box,
    // and the installer asks about it itself.
    public static bool CanInstall(Context context)
    {
        return !OperatingSystem.IsAndroidVersionAtLeast(26)
               || (context.PackageManager?.CanRequestPackageInstalls() ?? false);
    }

    // Android TV has no in-app route to this: the grant is a Settings screen, reached once.
    public static void AskToAllowInstalls(Context context)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26)) return;

        context.StartActivity(
            new Intent(Settings.ActionManageUnknownAppSources, Android.Net.Uri.Parse($"package:{context.PackageName}"))
                .AddFlags(ActivityFlags.NewTask));
    }

    public static void Install(Context context, string apkPath)
    {
        var installer = context.PackageManager!.PackageInstaller;
        var session = installer.OpenSession(
            installer.CreateSession(new PackageInstaller.SessionParams(PackageInstallMode.FullInstall)));

        using (session)
        {
            var apk = new FileInfo(apkPath);

            using (var destination = session.OpenWrite(SessionName, 0, apk.Length))
            using (var source = File.OpenRead(apkPath))
            {
                source.CopyTo(destination);
                session.Fsync(destination);
            }

            session.Commit(StatusIntent(context).IntentSender!);
        }
    }

    // The session fills the confirmation intent in on the way back, so this one cannot be
    // immutable; from Android 12 that has to be said out loud.
    private static PendingIntent StatusIntent(Context context)
    {
        var flags = OperatingSystem.IsAndroidVersionAtLeast(31)
            ? PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Mutable
            : PendingIntentFlags.UpdateCurrent;

        return PendingIntent.GetBroadcast(
            context,
            0,
            new Intent(context, typeof(ApkInstallReceiver)).SetPackage(context.PackageName),
            flags)!;
    }
}
