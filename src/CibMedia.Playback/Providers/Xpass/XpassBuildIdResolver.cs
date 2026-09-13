using CibMedia.Playback.Logging;
using System.Text.RegularExpressions;
using CibMedia.Playback;
using Microsoft.Extensions.Caching.Memory;

namespace CibMedia.Playback.Providers.Xpass;

internal sealed partial class XpassBuildIdResolver(
    HttpClient http,
    IMemoryCache cache,
    XpassOptions options,
    ILogger<XpassBuildIdResolver> logger
)
{
    private const string CacheKey = "playback:xpass:build-id";
    private const string BuildIdPrefix = "spv3-build-";

    // Written on a cold cache and after a failed decrypt, so a stale entry costs one refresh, not
    // an outage.
    private static readonly TimeSpan Ttl = TimeSpan.FromDays(30);

    // The walk is two more downloads of bundles that run to megabytes. Its callers start it off
    // their own budget on purpose — a walk cut short is one the next title repeats — so without
    // this it would have none but the client's per-request timeout, and two of those in a row put
    // a cold lookup past twenty seconds before a single server had been probed.
    private static readonly TimeSpan WalkBudget = TimeSpan.FromSeconds(8);

    // The build id keying the decrypt: cached, or extracted from the live bundles when the cache is
    // cold. Null when the bundles no longer match the extraction recipe.
    public async Task<string?> GetAsync(string pageHtml, CancellationToken cancellationToken)
    {
        var cached = cache.Get<string>(CacheKey);

        return string.IsNullOrWhiteSpace(cached) ? await RefreshAsync(pageHtml, cancellationToken) : cached;
    }

    public async Task<string?> RefreshAsync(string pageHtml, CancellationToken cancellationToken)
    {
        using var walk = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        walk.CancelAfter(WalkBudget);

        try
        {
            return await WalkAsync(pageHtml, walk.Token);
        }
        catch (OperationCanceledException)
            when (walk.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            logger.XpassBuildIdUnfinished(WalkBudget);

            return null;
        }
    }

    // Walks page → mainmini.js → player bundle. The player script has a random per-deploy name that
    // exists only inside mainmini's string table, and the build id only inside the player's.
    private async Task<string?> WalkAsync(string pageHtml, CancellationToken cancellationToken)
    {
        // Error rather than warning: each of these means the extraction recipe no longer matches
        // the bundles, and xpass stays dark until the code changes.
        var loader = MainMiniRegex().Match(pageHtml);
        if (!loader.Success)
        {
            logger.XpassBuildIdUnreadable("the page no longer references mainmini.js");

            return null;
        }

        var loaderSource = await http.GetStringAsync(Absolute(loader.Groups[1].Value), cancellationToken);
        var playerPath = XpassObfuscatedBundle.DecodeStrings(loaderSource)
            .Concat(XpassObfuscatedBundle.ReadLiterals(loaderSource))
            .FirstOrDefault(IsPlayerScript);
        if (playerPath is null)
        {
            logger.XpassBuildIdUnreadable("mainmini.js string table has no player script path");

            return null;
        }

        var playerSource = await http.GetStringAsync(Absolute(playerPath), cancellationToken);
        var buildId = XpassObfuscatedBundle.DecodeStrings(playerSource)
            .FirstOrDefault(value => value.StartsWith(BuildIdPrefix, StringComparison.Ordinal));
        if (buildId is null)
        {
            logger.XpassBuildIdUnreadable("the player bundle string table has no build id");

            return null;
        }

        cache.Set(CacheKey, buildId, Ttl);

        logger.XpassBuildIdRefreshed(buildId);

        return buildId;
    }

    private string Absolute(string path)
    {
        return $"https://{options.PlayerHost}{path}";
    }

    private static bool IsPlayerScript(string value)
    {
        return PlayerScriptRegex().IsMatch(value)
               && !value.Contains("mainmini", StringComparison.Ordinal)
               && !value.Contains("sdbmini", StringComparison.Ordinal);
    }

    [GeneratedRegex(@"src=""(/static/mainmini\.js[^""]*)""")]
    private static partial Regex MainMiniRegex();

    [GeneratedRegex(@"^/static/[A-Za-z0-9]+\.js(\?|$)")]
    private static partial Regex PlayerScriptRegex();
}