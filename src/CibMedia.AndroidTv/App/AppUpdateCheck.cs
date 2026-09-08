using System.Diagnostics;
using Android.Content;
using AndroidX.Core.Content.PM;
using CibMedia.Core.Abstractions;
using CibMedia.Core.Updates;
using CibMedia.AndroidTv.Platform;
using Microsoft.Extensions.Logging;

namespace CibMedia.AndroidTv.App;

// Off the launch path: config is written as it arrives and read by the next launch, and an apk is
// only ever offered. Every foreground asks, so a box that is never killed still hears about a
// release; the interval is what stops it asking on every trip back from a details page.
internal sealed class AppUpdateCheck(
    Context context,
    IAppUpdates updates,
    IAppUpdatePreferences preferences,
    HttpClient client,
    ILogger<AppUpdateCheck> log)
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(4);

    // Monotonic: a box syncs its clock at boot, and a wall clock that jumps takes the interval with it.
    private long _asked;

    public AppManifest? Offered { get; private set; }

    public string InstalledVersionName =>
        context.PackageManager?.GetPackageInfo(context.PackageName!, 0)?.VersionName ?? "";

    public async Task<UpdateCheckResult> CheckAsync(bool force)
    {
        if (!force && _asked != 0 && Stopwatch.GetElapsedTime(_asked) < Interval)
        {
            return UpdateCheckResult.Skipped;
        }

        _asked = Stopwatch.GetTimestamp();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        if (await updates.ReadAsync(timeout.Token).ConfigureAwait(true) is not { } manifest)
        {
            log.LogWarning("No manifest: the box keeps the apk and the config it has.");

            return UpdateCheckResult.Unreachable;
        }

        if (manifest.PlaybackJson is { } json && PlaybackConfig.Write(context, json))
        {
            log.LogInformation("playback.json replaced from the manifest; it is read next launch.");
        }

        if (!manifest.ShouldOffer(InstalledVersionCode(), preferences.DeclinedVersionCode))
        {
            return UpdateCheckResult.UpToDate;
        }

        Offered = manifest;

        return UpdateCheckResult.Offered;
    }

    public Task<string?> DownloadAsync(AppManifest manifest)
    {
        return ApkDownload.FetchAsync(context, client, manifest, log, CancellationToken.None);
    }

    public void Decline()
    {
        if (Offered is { } offered) preferences.DeclinedVersionCode = offered.VersionCode;
    }

    private int InstalledVersionCode()
    {
        var installed = context.PackageManager?.GetPackageInfo(context.PackageName!, 0);

        return installed is null ? 0 : (int)PackageInfoCompat.GetLongVersionCode(installed);
    }
}
