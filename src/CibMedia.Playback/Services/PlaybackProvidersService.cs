using CibMedia.Playback.Models;
using CibMedia.Playback.Providers;

namespace CibMedia.Playback.Services;

// In the order their sources are offered to the player.
public sealed class PlaybackProvidersService
{
    internal PlaybackProvidersService(IEnumerable<ITmdbPlaybackProvider> providers)
    {
        Available =
        [
            .. providers
                .Where(provider => provider.Enabled)
                .Select(provider => provider.Provider)
        ];
    }

    public IReadOnlyList<PlaybackProvider> Available { get; }
}
