using System.Text.Json;
using Android.Content;
using CibMedia.Core.Infrastructure.Playback;
using CibMedia.Playback;

namespace CibMedia.AndroidTv.Platform;

// playback.json as the box sees it: one the updater wrote, else the one the apk installed.
internal static class PlaybackConfig
{
    private const string Name = "playback.json";

    public static PlaybackOptions Read(Context context)
    {
        using var stream = Fetched(context) is { } fetched
            ? System.IO.File.OpenRead(fetched)
            : context.Assets!.Open(Name);

        using var reader = new StreamReader(stream);

        return JsonSerializer.Deserialize<PlaybackOptions>(reader.ReadToEnd(), JsonSerializerOptions.Web)
               ?? throw new InvalidOperationException($"{Name} is empty.");
    }

    // Written whole under another name and moved into place, so a download cut halfway through
    // cannot become the document the next launch reads. False when what arrived is not one.
    public static bool Write(Context context, string json)
    {
        var path = Path.Combine(context.FilesDir!.AbsolutePath, Name);

        if (System.IO.File.Exists(path) && System.IO.File.ReadAllText(path) == json) return false;

        if (!PlaybackDocument.IsUsable(json)) return false;

        var pending = path + ".pending";

        System.IO.File.WriteAllText(pending, json);
        System.IO.File.Move(pending, path, overwrite: true);

        return true;
    }

    private static string? Fetched(Context context)
    {
        var path = Path.Combine(context.FilesDir!.AbsolutePath, Name);

        return System.IO.File.Exists(path) ? path : null;
    }
}
