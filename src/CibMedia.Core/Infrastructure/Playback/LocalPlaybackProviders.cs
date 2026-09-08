using CibMedia.Core.Abstractions;
using CibMedia.Playback.Services;

namespace CibMedia.Core.Infrastructure.Playback;

public sealed class LocalPlaybackProviders(PlaybackProvidersService providers) : IPlaybackProviders
{
    public Task<IReadOnlyList<string>> NamesAsync(CancellationToken ct)
    {
        return Task.FromResult(providers.Available.ToNames());
    }
}
