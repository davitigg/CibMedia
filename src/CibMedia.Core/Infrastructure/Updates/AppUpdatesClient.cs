using System.Text.Json;
using CibMedia.Core.Abstractions;
using CibMedia.Core.Updates;

namespace CibMedia.Core.Infrastructure.Updates;

// Read field by field rather than deserialised: the playback block has to survive as the text it
// was published as, and a manifest that gains a field must not fail the boxes that predate it.
public sealed class AppUpdatesClient(HttpClient client, Uri manifestUrl) : IAppUpdates
{
    public async Task<AppManifest?> ReadAsync(CancellationToken ct)
    {
        try
        {
            return Parse(await client.GetStringAsync(manifestUrl, ct).ConfigureAwait(false));
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException
                                              or JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    internal static AppManifest? Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.ValueKind is not JsonValueKind.Object
            || !root.TryGetProperty("versionCode", out var versionCode)
            || versionCode.ValueKind is not JsonValueKind.Number)
        {
            return null;
        }

        return new AppManifest
        {
            VersionCode = versionCode.GetInt32(),
            VersionName = Text(root, "versionName"),
            ApkUrl = Text(root, "apkUrl"),
            Sha256 = Text(root, "sha256"),
            SizeBytes = root.TryGetProperty("sizeBytes", out var size)
                        && size.ValueKind is JsonValueKind.Number
                ? size.GetInt64()
                : 0,
            Notes = Text(root, "notes"),
            PlaybackJson = root.TryGetProperty("playback", out var playback)
                           && playback.ValueKind is JsonValueKind.Object
                ? playback.GetRawText()
                : null,
        };
    }

    private static string Text(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
    }
}
