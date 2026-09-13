using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace CibMedia.Playback.Extensions;

internal static class MemoryCacheExtensions
{
    // One lookup runs at a time per key. Opening a title's details and pressing play resolve it
    // separately and overlap, and measured on the box the second doubled every request the first
    // was already making — enough contention to push it past the client's own timeout and open an
    // outage window on a provider that had just answered. The second caller now waits on the first.
    public static async ValueTask<T> GetOrStoreAsync<T>(
        this IMemoryCache cache,
        string key,
        Func<CancellationToken, ValueTask<T>> factory,
        Func<T, TimeSpan> lifetime,
        CancellationToken cancellationToken
    )
    {
        if (cache.TryGetValue<T>(key, out var cached)) return cached!;

        var flight = Flights<T>.InFlight.GetOrAdd(
            key,
            entry => new Lazy<Task<T>>(
                () => RunAsync(cache, entry, factory, lifetime),
                LazyThreadSafetyMode.ExecutionAndPublication));

        // The shared run is nobody's to cancel, so a caller that gives up stops waiting and leaves
        // it to finish for whoever else is on it.
        return await flight.Value.WaitAsync(cancellationToken);
    }

    private static async Task<T> RunAsync<T>(
        IMemoryCache cache,
        string key,
        Func<CancellationToken, ValueTask<T>> factory,
        Func<T, TimeSpan> lifetime
    )
    {
        try
        {
            var value = await factory(CancellationToken.None);

            // The answer decides how long it is worth holding: what an upstream carries and what it
            // merely failed to prove today are not good for the same span.
            cache.Set(key, value, lifetime(value));

            return value;
        }
        finally
        {
            // After the set, so a caller arriving in between reads the answer rather than starting
            // the work again. A throw leaves nothing behind and the next caller retries.
            Flights<T>.InFlight.TryRemove(key, out _);
        }
    }

    private static class Flights<T>
    {
        public static readonly ConcurrentDictionary<string, Lazy<Task<T>>> InFlight = new();
    }
}
