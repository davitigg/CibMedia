namespace CibMedia.Playback.Providers;

// Both stacks 403 a request without a browser agent: the subtitle host refuses calls that carry
// none, and the MP4 CDN refuses the player even when the Referer is right.
internal static class BrowserUserAgent
{
    public const string Value =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) " +
        "Chrome/120.0.0.0 Safari/537.36";
}