using System.Text.Json;
using CibMedia.Playback;
using Microsoft.Extensions.DependencyInjection;

namespace CibMedia.Core.Infrastructure.Playback;

// A fetched document is kept only if the registration the next launch runs accepts it. Anything
// weaker leaves a box that starts, reads what was written, and throws before it draws a screen —
// with no way left to send it a correction.
public static class PlaybackDocument
{
    public static bool IsUsable(string json)
    {
        try
        {
            if (JsonSerializer.Deserialize<PlaybackOptions>(json, JsonSerializerOptions.Web) is not { } options)
            {
                return false;
            }

            new ServiceCollection().AddPlayback(options);

            return true;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return false;
        }
    }
}
