using Android.Content;
using AndroidX.Core.Content.PM;
using CibMedia.Core.Abstractions;
using CibMedia.Core.Updates;
using CibMedia.AndroidTv.Platform;
using Microsoft.Extensions.Logging;

namespace CibMedia.AndroidTv.App;

// Off the launch path and once a process. Config is written as soon as it arrives and read by
// the next launch; an apk is only ever offered, never taken without an answer.
internal sealed class AppUpdateCheck(
    Context context,
    IAppUpdates updates,
    IAppUpdatePreferences preferences,
    HttpClient client,
    ILogger<AppUpdateCheck> log)
{
    private int _started;

    public AppManifest? Offered { get; private set; }

    public async Task<bool> RunOnceAsync()
    {
        if (Interlocked.Exchange(ref _started, 1) == 1) return false;

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        if (await updates.ReadAsync(timeout.Token).ConfigureAwait(true) is not { } manifest)
        {
            log.LogDebug("No manifest: the box keeps the apk and the config it has.");
            return false;
        }

        if (manifest.PlaybackJson is { } json && PlaybackConfig.Write(context, json))
        {
            log.LogInformation("playback.json replaced from the manifest; it is read next launch.");
        }

        if (!manifest.ShouldOffer(InstalledVersionCode(), preferences.DeclinedVersionCode)) return false;

        Offered = manifest;

        return true;
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
