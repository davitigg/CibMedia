using System.Security.Cryptography;
using Android.Content;
using CibMedia.Core.Updates;
using Microsoft.Extensions.Logging;

namespace CibMedia.AndroidTv.Platform;

// Fetched to the cache and hashed on the way in, so nothing that is not what the manifest
// published is ever handed to the installer.
internal static class ApkDownload
{
    public static async Task<string?> FetchAsync(
        Context context,
        HttpClient client,
        AppManifest manifest,
        ILogger log,
        CancellationToken ct)
    {
        var path = Path.Combine(context.CacheDir!.AbsolutePath, $"update-{manifest.VersionCode}.apk");

        try
        {
            await using (var upstream = await client.GetStreamAsync(manifest.ApkUrl, ct).ConfigureAwait(false))
            await using (var file = File.Create(path))
            {
                await upstream.CopyToAsync(file, ct).ConfigureAwait(false);
            }

            var written = new FileInfo(path).Length;
            var checksum = await ChecksumAsync(path, ct).ConfigureAwait(false);

            if (manifest.MatchesChecksum(checksum)) return path;

            log.LogWarning(
                "Update {Version} discarded: {Written} bytes of {Published}, sha256 {Checksum} not {Expected}.",
                manifest.VersionCode, written, manifest.SizeBytes, checksum, manifest.Sha256);

            return Discard(path);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or TaskCanceledException)
        {
            log.LogWarning(exception, "Update {Version} could not be fetched.", manifest.VersionCode);

            return Discard(path);
        }
    }

    private static async Task<string> ChecksumAsync(string path, CancellationToken ct)
    {
        await using var file = File.OpenRead(path);

        return Convert.ToHexString(await SHA256.HashDataAsync(file, ct).ConfigureAwait(false));
    }

    private static string? Discard(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // A file the box cannot delete is one the installer is never pointed at anyway.
        }

        return null;
    }
}
