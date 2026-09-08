using CibMedia.Playback.Logging;
using System.Diagnostics.CodeAnalysis;

namespace CibMedia.Playback.Providers.Liveball;

// The box fetches the page, and the address arrives from whatever posted a play command, so
// admitting any host would make the app a proxy for anything on the network that can reach it.
internal sealed class LiveballPageUrls(LiveballOptions options, ILogger<LiveballPageUrls> logger)
{
    public bool TryParse(string? url, [NotNullWhen(true)] out Uri? page)
    {
        page = Normalize(url);

        if (page is null) logger.LiveballPageRejected(url);

        return page is not null;
    }

    private Uri? Normalize(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var candidate)) return null;
        if (candidate.Scheme != Uri.UriSchemeHttps && candidate.Scheme != Uri.UriSchemeHttp) return null;

        // A non-default port would reach a service other than the site itself on an allowed name.
        if (!candidate.IsDefaultPort || !IsLiveball(candidate.Host)) return null;

        // Query and fragment do not change what liveball renders; dropping them keeps one cache
        // entry per page. Credentials go with them.
        return new UriBuilder(candidate)
        {
            Scheme = Uri.UriSchemeHttps,
            Port = -1,
            UserName = "",
            Password = "",
            Query = "",
            Fragment = ""
        }.Uri;
    }

    private bool IsLiveball(string host)
    {
        return options.PageHosts.Any(allowed =>
            host.Equals(allowed, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith($".{allowed}", StringComparison.OrdinalIgnoreCase));
    }
}