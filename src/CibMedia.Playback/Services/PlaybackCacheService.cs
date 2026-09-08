using Microsoft.Extensions.Caching.Memory;

namespace CibMedia.Playback.Services;

public sealed class PlaybackCacheService
{
    private readonly IMemoryCache _cache;

    internal PlaybackCacheService(IMemoryCache cache)
    {
        _cache = cache;
    }

    // Only the concrete cache can drop every entry at once. A host that registered its own before
    // AddPlayback ran keeps whatever policy it chose.
    public void Clear()
    {
        (_cache as MemoryCache)?.Clear();
    }
}
