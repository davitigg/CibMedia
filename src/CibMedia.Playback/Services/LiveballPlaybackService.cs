using CibMedia.Playback.Logging;
using System.Diagnostics;
using CibMedia.Playback.Models;
using CibMedia.Playback.Providers.Liveball;
using Microsoft.Extensions.Caching.Memory;

namespace CibMedia.Playback.Services;

// Live sport addressed by page rather than a title by TMDB id, so it has its own service instead
// of a provider under the TMDB one.
public sealed class LiveballPlaybackService
{
    // Far below the TMDB source TTL: a channel coming on or going off air changes the answer, and
    // the media token lasts hours, so a cached URL never lapses inside this.
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    private readonly IMemoryCache _cache;
    private readonly LiveballClient _client;
    private readonly LiveballKeyResolver _keys;
    private readonly ILogger<LiveballPlaybackService> _logger;
    private readonly LiveballOptions _options;
    private readonly LiveballPageUrls _pages;

    internal LiveballPlaybackService(
        LiveballClient client,
        LiveballKeyResolver keys,
        LiveballPageUrls pages,
        LiveballOptions options,
        IMemoryCache cache,
        ILogger<LiveballPlaybackService> logger
    )
    {
        _client = client;
        _keys = keys;
        _pages = pages;
        _options = options;
        _cache = cache;
        _logger = logger;
    }

    private bool Enabled => _options.Enabled;

    // Null is the ordinary answer, not an error: liveball keeps scores for far more fixtures than
    // its handful of channels can carry, and a dark page is cached like any other. An address this
    // does not recognise reads the same way — neither is something to play.
    public async Task<ResolvedPlayback?> GetAsync(string url, CancellationToken cancellationToken)
    {
        if (!Enabled || !_pages.TryParse(url, out var page)) return null;

        var started = Stopwatch.GetTimestamp();

        var response = await _cache.GetOrCreateAsync(
            $"playback:liveball:{page}",
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = Ttl;

                return await ResolveAsync(page, cancellationToken);
            });

        var elapsed = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;

        // A page carries one source, so its stream count is the channel count.
        if (response is null) _logger.LiveballDark(page, elapsed);
        else _logger.LiveballResolved(page, response.Sources[0].Streams.Count, elapsed);

        return response;
    }

    private async Task<ResolvedPlayback?> ResolveAsync(Uri page, CancellationToken cancellationToken)
    {
        var html = await _client.GetPageAsync(page, cancellationToken);

        var encoded = LiveballPage.ReadPayloads(html);
        if (encoded.Count is 0) return null;

        // Thrown rather than skipped: a payload that is not even base64 is the page changing shape,
        // and a miss here would be cached.
        var payloads = encoded.Select(LiveballToken.ReadPayload).OfType<byte[]>().ToList();
        if (payloads.Count != encoded.Count)
            throw new InvalidOperationException($"A liveball payload on {page} is not base64.");

        var feeds = await DecodeAsync(page, payloads, cancellationToken);

        // Each feed costs a resolve and a probe; run together so a two-channel page is not four
        // sequential hops on top of the page fetch.
        var outcomes = await Task.WhenAll(feeds.Select(feed => ResolveFeedAsync(page, feed, cancellationToken)));

        var streams = outcomes.Select(outcome => outcome.Stream).OfType<PlaybackStream>().ToList();
        if (streams.Count > 0) return Playable(streams);

        // A page that advertised feeds and yielded none through faults is a broken handoff, not an
        // event nobody is broadcasting. Thrown so it is not held as a miss for the TTL. A dark
        // channel beside a faulted one costs only that channel.
        var failures = outcomes.Select(outcome => outcome.Error).OfType<Exception>().ToList();

        return failures.Count is 0
            ? null
            : throw new AggregateException($"No liveball feed on {page} resolved.", failures);
    }

    // Nothing decoding is what a rotated key looks like from here, so the key is recovered from live
    // payloads before the page is given up on. Some decoding means the key is right and the rest is
    // a payload liveball got wrong.
    private async Task<IReadOnlyList<Feed>> DecodeAsync(
        Uri page,
        List<byte[]> payloads,
        CancellationToken cancellationToken
    )
    {
        var key = _keys.Get();
        var feeds = key is null ? [] : Decode(payloads, key);

        if (feeds.Count is 0)
        {
            key = await _keys.RecoverAsync(payloads, cancellationToken);
            feeds = key is null ? [] : Decode(payloads, key);

            if (feeds.Count is 0)
                throw new InvalidOperationException(
                    $"liveball payloads on {page} did not decode with any recoverable key.");
        }

        if (feeds.Count < payloads.Count)
            _logger.LiveballPayloadsUndecoded(page, payloads.Count - feeds.Count, payloads.Count);

        return feeds;
    }

    private static List<Feed> Decode(List<byte[]> payloads, byte[] key)
    {
        var feeds = new List<Feed>(payloads.Count);

        foreach (var payload in payloads)
        {
            var token = LiveballToken.Decode(payload, key);
            var channel = LiveballToken.ReadChannel(token);
            if (channel is not null) feeds.Add(new Feed(token, channel));
        }

        return feeds;
    }

    // One source, since every channel comes off the same stack with the same empty headers; page
    // order is the site's own tab order. Channels are independent live timelines, so switching lands
    // on the other channel's live edge. No headers: the edge serves bare requests. No subtitles:
    // liveball carries none.
    private static ResolvedPlayback Playable(IReadOnlyList<PlaybackStream> streams)
    {
        var source = new PlaybackSource(PlaybackProvider.Liveball, new Dictionary<string, string>(), streams, []);

        return new ResolvedPlayback(PlaybackMediaType.Livestream, [source]);
    }

    private async Task<FeedOutcome> ResolveFeedAsync(Uri page, Feed feed, CancellationToken cancellationToken)
    {
        try
        {
            var url = await _client.ResolveAsync(page, feed.Token, cancellationToken);
            if (url is null || !await _client.IsOnAirAsync(url, cancellationToken)) return FeedOutcome.Dark;

            return new FeedOutcome(new HlsStream(url, feed.Channel), null);
        }
        catch (Exception exception) when (LiveballClient.IsUpstreamFault(exception, cancellationToken))
        {
            _logger.LiveballChannelFailed(feed.Channel, page, exception);

            return new FeedOutcome(null, exception);
        }
    }

    private sealed record Feed(string Token, string Channel);

    private sealed record FeedOutcome(PlaybackStream? Stream, Exception? Error)
    {
        public static readonly FeedOutcome Dark = new(null, null);
    }
}