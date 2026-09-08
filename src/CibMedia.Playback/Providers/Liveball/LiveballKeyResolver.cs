using CibMedia.Playback.Extensions;
using CibMedia.Playback.Logging;
using CibMedia.Playback;
using Microsoft.Extensions.Caching.Memory;

namespace CibMedia.Playback.Providers.Liveball;

internal sealed class LiveballKeyResolver(
    LiveballClient client,
    LiveballOptions options,
    IMemoryCache cache,
    ILogger<LiveballKeyResolver> logger
)
{
    private const string CacheKey = "playback:liveball:token-key";

    // Written on a cold cache and after a failed decode, so a stale entry costs one recovery, not
    // an outage.
    private static readonly TimeSpan Ttl = TimeSpan.FromDays(30);

    public byte[]? Get()
    {
        return cache.Get<byte[]>(CacheKey);
    }

    // The page being resolved rarely carries enough on its own, so the configured channel pages are
    // read alongside it. Null when what was gathered still does not pin the key down.
    public async Task<byte[]?> RecoverAsync(IReadOnlyList<byte[]> payloads, CancellationToken cancellationToken)
    {
        var gathered = await Task.WhenAll(options.KeyPages.Select(page => ReadPayloadsAsync(page, cancellationToken)));

        var samples = payloads
            .Concat(gathered.SelectMany(page => page))
            .DistinctBy(Convert.ToBase64String)
            .ToList();

        var key = LiveballToken.RecoverKey(samples);
        if (key is null)
        {
            logger.LiveballKeyUnrecoverable(samples.Count);

            return null;
        }

        cache.Set(CacheKey, key, Ttl);

        logger.LiveballKeyRecovered(samples.Count);

        return key;
    }

    private async Task<IReadOnlyList<byte[]>> ReadPayloadsAsync(Uri page, CancellationToken cancellationToken)
    {
        try
        {
            var html = await client.GetPageAsync(page, cancellationToken);

            return LiveballPage.ReadPayloads(html).Select(LiveballToken.ReadPayload).OfType<byte[]>().ToList();
        }
        catch (Exception exception) when (exception.IsUpstreamFault(cancellationToken))
        {
            logger.LiveballKeyPageUnread(page, exception);

            return [];
        }
    }
}