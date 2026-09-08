using CibMedia.Playback.Logging;
using System.Text;
using System.Text.Json;
using CibMedia.Playback.Providers.Liveball.Models;

namespace CibMedia.Playback.Providers.Liveball;

// The three hops behind a lookup: the page, the resolve that trades a token for a media URL, and
// the playlist that says whether the channel is on air.
internal sealed class LiveballClient(HttpClient http, ILogger<LiveballClient> logger)
{
    private const string ResolvePath = "/api/c/r";
    private const string HlsMode = "h";
    private const string PlaylistHeader = "#EXTM3U";
    private const int SnippetLength = 300;

    private static readonly string[] CloudflareHeaders = ["cf-mitigated", "cf-ray", "server"];

    // The client's own timeout surfaces as a cancellation the caller never asked for.
    public static bool IsUpstreamFault(Exception exception, CancellationToken cancellationToken)
    {
        return exception is HttpRequestException
               || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested);
    }

    // Throws on a non-success status rather than returning null: an absent broadcast is cached as
    // a miss, and a Cloudflare challenge arriving as a 403 must not be held that way.
    public async Task<string> GetPageAsync(Uri page, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(page, cancellationToken);

        // Decoded explicitly: the page declares UTF-8, yet the charset the response negotiates
        // mangles the Cyrillic titles.
        var body = Encoding.UTF8.GetString(await response.Content.ReadAsByteArrayAsync(cancellationToken));

        return response.IsSuccessStatusCode
            ? body
            : throw new HttpRequestException(Refusal(page, response, body), null, response.StatusCode);
    }

    // A block by bot score, by region and by WAF rule are all the same 403 from here. Cloudflare
    // names which one fired in cf-mitigated, and the suffix of cf-ray is the PoP that answered,
    // which is the only thing on the wire that says where the caller looked like it came from.
    private static string Refusal(Uri page, HttpResponseMessage response, string body)
    {
        var marks = CloudflareHeaders
            .Select(name => response.Headers.TryGetValues(name, out var values)
                ? $"{name}: {string.Join(",", values)}"
                : null)
            .OfType<string>()
            .ToList();

        var reported = marks.Count is 0 ? "no cloudflare headers" : string.Join("; ", marks);

        return $"liveball answered {page} with {(int)response.StatusCode}. {reported}. Body: {Snippet(body)}";
    }

    // The block page is HTML across many lines, and the console writes one line per entry.
    private static string Snippet(string body)
    {
        var text = string.Join(' ', body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        return text.Length <= SnippetLength ? text : text[..SnippetLength];
    }

    // The media URL, or null when upstream answered with nothing a player can take: mode "f" is a
    // third-party iframe.
    public async Task<string?> ResolveAsync(Uri page, string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(page, ResolvePath))
        {
            Content = JsonContent.Create(
                new LiveballResolveRequest(token, Fingerprint()),
                options: JsonSerializerOptions.Web)
        };

        // Without both, the managed challenge fires even on a good token.
        request.Headers.Referrer = page;
        request.Headers.Add("Origin", page.GetLeftPart(UriPartial.Authority));

        using var response = await http.SendAsync(request, cancellationToken);

        // Read before any status check: a refused token is a 403 with the reason in the body.
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        LiveballResolveResponse? resolved;

        try
        {
            resolved = JsonSerializer.Deserialize<LiveballResolveResponse>(body, JsonSerializerOptions.Web);
        }
        catch (JsonException exception)
        {
            throw new HttpRequestException(
                $"liveball answered the resolve for {page} with {(int)response.StatusCode} and no JSON.",
                exception);
        }

        // A refusal means this side of the handoff is wrong, most likely a rotated key, not that
        // nobody is broadcasting; reporting it as an absent broadcast is how a rotation would go
        // unseen.
        if (resolved?.E is not null)
            throw new HttpRequestException($"liveball refused the resolve for {page}: {resolved.E}.");

        return resolved?.D is not null && string.Equals(resolved.M, HlsMode, StringComparison.OrdinalIgnoreCase)
            ? ReadMediaUrl(resolved.D)
            : null;
    }

    // The resolve mints tokens for dark channels too, and an idle edge answers an HTML "Not found",
    // at times under a 200, so only the playlist body says whether there is anything to play.
    public async Task<bool> IsOnAirAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode) return false;

            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            return body.StartsWith(PlaylistHeader, StringComparison.Ordinal);
        }
        catch (Exception exception) when (IsUpstreamFault(exception, cancellationToken))
        {
            // An edge that cannot be reached is off air as far as a player is concerned.
            logger.LiveballProbeFailed(new Uri(url).GetLeftPart(UriPartial.Path), exception);

            return false;
        }
    }

    private static string? ReadMediaUrl(string encoded)
    {
        var buffer = new byte[encoded.Length];
        if (!Convert.TryFromBase64String(encoded, buffer, out var written)) return null;

        var url = Encoding.UTF8.GetString(buffer, 0, written);

        return Uri.TryCreate(url, UriKind.Absolute, out var media)
               && (media.Scheme == Uri.UriSchemeHttps || media.Scheme == Uri.UriSchemeHttp)
            ? url
            : null;
    }

    // Upstream checks only that the field is present: a missing one is rejected, a fabricated one
    // in the site's doubled-eight-hex shape is not.
    private static string Fingerprint()
    {
        var half = Guid.NewGuid().ToString("N")[..8];

        return half + half;
    }
}