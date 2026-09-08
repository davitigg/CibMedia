using Android.Content;
using Bumptech.Glide;

namespace CibMedia.AndroidTv.Platform;

// Glide keeps its own disk cache beside FileCacheStore's, so "clear cache" has to reach both.
public static class ImageCache
{
    public static Task<long> SizeAsync(Context context)
    {
        var directory = Glide.GetPhotoCacheDir(context)?.AbsolutePath;

        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return Task.FromResult(0L);

        return Task.Run(() => Bytes(directory));
    }

    // Glide asserts on the thread in opposite directions: ClearMemory must be on the main
    // thread and ClearDiskCache off it.
    public static Task ClearAsync(Context context)
    {
        var glide = Glide.Get(context);

        glide.ClearMemory();

        return Task.Run(() => glide.ClearDiskCache());
    }

    private static long Bytes(string directory)
    {
        try
        {
            return new DirectoryInfo(directory)
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Sum(file => file.Length);
        }
        catch (IOException)
        {
            return 0L;
        }
        catch (UnauthorizedAccessException)
        {
            return 0L;
        }
    }
}
