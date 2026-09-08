using Microsoft.Extensions.Caching.Memory;

namespace CibMedia.Playback.Extensions;

internal static class MemoryCacheExtensions
{
    // For entries whose lifetime the value decides: what came back picks how long it is held.
    public static async ValueTask<T> GetOrStoreAsync<T>(
        this IMemoryCache cache,
        string key,
        Func<CancellationToken, ValueTask<T>> factory,
        Func<T, TimeSpan> lifetime,
        CancellationToken cancellationToken
    )
    {
        if (cache.TryGetValue<T>(key, out var cached)) return cached!;

        var value = await factory(cancellationToken);

        cache.Set(key, value, lifetime(value));

        return value;
    }
}
