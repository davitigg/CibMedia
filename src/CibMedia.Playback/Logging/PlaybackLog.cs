using CibMedia.Playback.Models;

namespace CibMedia.Playback.Logging;

// Every line the playback feature can write, in one place: wording, level and event id are only
// comparable to each other if they sit next to each other. 1xxx is one line per request whatever
// the outcome, so a quiet log reads as an idle box rather than a broken one; 2xxx is which stack is
// unhealthy; 3xxx is an upstream changing shape.
internal static partial class PlaybackLog
{
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "playback for {Lookup}: {SourceCount} of {ProviderCount} provider(s) answered ({Tally}) in {ElapsedMs}ms")]
    public static partial void PlaybackResolved(
        this ILogger logger,
        string lookup,
        int sourceCount,
        int providerCount,
        string tally,
        long elapsedMs
    );

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Information,
        Message = "playback for {Lookup}: no provider carries it ({ElapsedMs}ms)")]
    public static partial void PlaybackMissing(this ILogger logger, string lookup, long elapsedMs);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Information,
        Message = "liveball {Page}: {StreamCount} channel(s) on air in {ElapsedMs}ms")]
    public static partial void LiveballResolved(this ILogger logger, Uri page, int streamCount, long elapsedMs);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Information,
        Message = "liveball {Page}: nothing on air ({ElapsedMs}ms)")]
    public static partial void LiveballDark(this ILogger logger, Uri page, long elapsedMs);

    // The status alone does not say which rule the address broke.
    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Information,
        Message = "liveball {Url} rejected: not an allowed liveball page")]
    public static partial void LiveballPageRejected(this ILogger logger, string? url);

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Warning,
        Message = "provider {Provider} failed for {Lookup}")]
    public static partial void ProviderFailed(
        this ILogger logger,
        PlaybackProvider provider,
        string lookup,
        Exception exception
    );

    // Debug, because the window opening is reported once at warning: repeating it per request for
    // the whole window is the noise that buries the opening.
    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Debug,
        Message = "provider {Provider} skipped for {Lookup}: its outage window is open")]
    public static partial void ProviderSkipped(this ILogger logger, PlaybackProvider provider, string lookup);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Warning,
        Message = "provider {Provider} is unavailable; every lookup skips it for the next {Outage}")]
    public static partial void ProviderOutageOpened(
        this ILogger logger,
        PlaybackProvider provider,
        TimeSpan outage,
        Exception exception
    );

    // Debug: mirrors are raced, so a lost mirror only matters once the race itself fails, and that
    // surfaces as ProviderFailed.
    [LoggerMessage(
        EventId = 2004,
        Level = LogLevel.Debug,
        Message = "videodb mirror {Host} failed for {Lookup}")]
    public static partial void VideoDbMirrorFailed(
        this ILogger logger,
        string host,
        string lookup,
        Exception exception
    );

    [LoggerMessage(
        EventId = 2005,
        Level = LogLevel.Debug,
        Message = "xpass server {Server} did not verify")]
    public static partial void XpassServerUnverified(this ILogger logger, string? server, Exception exception);

    // A server the wave cut off is dropped as silently as one that failed, and the count that
    // reaches the player is the only other place it would show.
    [LoggerMessage(
        EventId = 2008,
        Level = LogLevel.Debug,
        Message = "xpass server {Server} did not finish before the wave deadline")]
    public static partial void XpassServerUnfinished(this ILogger logger, string? server);

    // Debug: liveball mints tokens for dark channels too, so an edge that cannot be reached is the
    // ordinary shape of a channel being off air.
    [LoggerMessage(
        EventId = 2006,
        Level = LogLevel.Debug,
        Message = "liveball playlist probe failed for {Playlist}")]
    public static partial void LiveballProbeFailed(this ILogger logger, string playlist, Exception exception);

    [LoggerMessage(
        EventId = 2007,
        Level = LogLevel.Warning,
        Message = "liveball channel {Channel} on {Page} failed to resolve")]
    public static partial void LiveballChannelFailed(
        this ILogger logger,
        string channel,
        Uri page,
        Exception exception
    );

    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Information,
        Message = "xpass decrypt failed under build {BuildId}; refreshing the build id")]
    public static partial void XpassBuildIdStale(this ILogger logger, string buildId);

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Information,
        Message = "xpass build id refreshed to {BuildId}")]
    public static partial void XpassBuildIdRefreshed(this ILogger logger, string buildId);

    // One event id for all three steps of the walk, so a single alert rule catches a redeployed
    // player whichever step stopped matching; {Reason} says which.
    [LoggerMessage(
        EventId = 3003,
        Level = LogLevel.Error,
        Message = "xpass build id extraction failed: {Reason}. xpass stays dark until the recipe is updated")]
    public static partial void XpassBuildIdUnreadable(this ILogger logger, string reason);

    [LoggerMessage(
        EventId = 3004,
        Level = LogLevel.Warning,
        Message = "xpass decrypt failed again under refreshed build {BuildId}; the payload shape has changed")]
    public static partial void XpassDecryptUnreadable(this ILogger logger, string buildId);

    [LoggerMessage(
        EventId = 3005,
        Level = LogLevel.Information,
        Message = "liveball token key recovered from {Count} payload(s)")]
    public static partial void LiveballKeyRecovered(this ILogger logger, int count);

    [LoggerMessage(
        EventId = 3006,
        Level = LogLevel.Error,
        Message =
            "liveball token key recovery failed across {Count} payload(s). liveball stays dark until a key is recoverable")]
    public static partial void LiveballKeyUnrecoverable(this ILogger logger, int count);

    // Debug: key pages are read best-effort and the recovery reports its own verdict.
    [LoggerMessage(
        EventId = 3007,
        Level = LogLevel.Debug,
        Message = "liveball key page {Page} could not be read")]
    public static partial void LiveballKeyPageUnread(this ILogger logger, Uri page, Exception exception);

    [LoggerMessage(
        EventId = 3008,
        Level = LogLevel.Warning,
        Message = "liveball {Page}: {Count} of {Total} payload(s) did not decode")]
    public static partial void LiveballPayloadsUndecoded(this ILogger logger, Uri page, int count, int total);
}