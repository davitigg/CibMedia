using CibMedia.Core.Abstractions;
using CibMedia.Playback.Services;

namespace CibMedia.Core.Infrastructure.Playback;

public sealed class LocalPlaybackCache(PlaybackCacheService cache) : IPlaybackCache
{
    public void Clear()
    {
        cache.Clear();
    }
}
