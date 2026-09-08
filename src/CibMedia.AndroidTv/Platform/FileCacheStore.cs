using System.Security.Cryptography;
using System.Text;
using Android.Content;
using CibMedia.Core.Abstractions;

namespace CibMedia.AndroidTv.Platform;

// One file per key under the cache dir, with freshness taken from the file's write time, so
// there is no index to keep in sync and Android can evict the directory under pressure.
public sealed class FileCacheStore : ICacheStore
{
    private readonly DirectoryInfo _directory;

    public FileCacheStore(Context context)
    {
        var root = context.CacheDir?.AbsolutePath ?? Path.GetTempPath();
        _directory = Directory.CreateDirectory(Path.Combine(root, "catalog"));
    }

    public async Task<byte[]?> ReadAsync(string key, TimeSpan maxAge, CancellationToken ct)
    {
        var file = new FileInfo(PathFor(key));

        if (!file.Exists || DateTime.UtcNow - file.LastWriteTimeUtc > maxAge) return null;

        try
        {
            return await File.ReadAllBytesAsync(file.FullName, ct).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return null;
        }
    }

    // Write-then-move, so a kill mid-write cannot leave a truncated entry.
    public async Task WriteAsync(string key, byte[] value, CancellationToken ct)
    {
        var path = PathFor(key);
        var temporary = path + ".tmp";

        await File.WriteAllBytesAsync(temporary, value, ct).ConfigureAwait(false);
        File.Move(temporary, path, true);
    }

    public Task<long> SizeAsync(CancellationToken ct)
    {
        return Task.Run(
            () =>
            {
                try
                {
                    return _directory.EnumerateFiles().Sum(file => file.Length);
                }
                catch (IOException)
                {
                    return 0L;
                }
            },
            ct);
    }

    public Task ClearAsync(CancellationToken ct)
    {
        foreach (var file in _directory.EnumerateFiles())
            try
            {
                file.Delete();
            }
            catch (IOException)
            {
            }

        return Task.CompletedTask;
    }

    private string PathFor(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Path.Combine(_directory.FullName, Convert.ToHexStringLower(hash));
    }
}
