using CibMedia.Playback.Logging;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CibMedia.Playback.Providers.Xpass.Models;

namespace CibMedia.Playback.Providers.Xpass;

internal sealed partial class XpassClient(
    HttpClient http,
    XpassBuildIdResolver buildIds,
    XpassOptions options,
    ILogger<XpassClient> logger
)
{
    private const int NonceLength = 12;
    private const int TagLength = 16;

    // The embed page and its payload are the only hops the resolver's budget does not cover, so
    // without this they run to the client's own timeout. Measured on the box: at ten seconds a
    // page that was merely slow read as an outage and shut the stack out for forty-five.
    private static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(6);

    // The servers the embed offers for a title, in the embed's own order, or empty when the title
    // is not in the catalogue.
    public async Task<IReadOnlyList<XpassServer>> GetServersAsync(string path, CancellationToken cancellationToken)
    {
        var pageUrl = $"https://{options.PlayerHost}/e/{path}?autostart=false";

        using var fetch = StartFetch(cancellationToken);
        var page = await http.GetStringAsync(pageUrl, fetch.Token);

        var dataUrl = DataUrlRegex().Match(page).Groups[1].Value;
        if (dataUrl.Length is 0) return [];

        var query = dataUrl.IndexOf('?');
        var pathname = query < 0 ? dataUrl : dataUrl[..query];
        var token = TokenRegex().Match(dataUrl).Groups[1].Value;
        if (token.Length is 0) return [];

        // The build id is a cache read, or on a cold cache the bundle walk; neither depends on the
        // payload, so it rides alongside the download.
        var buildIdTask = buildIds.GetAsync(page, cancellationToken);
        var ciphertext = await GetCiphertextAsync(dataUrl, pageUrl, cancellationToken);
        if (ciphertext.Length is 0) return [];

        var buildId = await buildIdTask;
        if (buildId is null) return [];

        var servers = Decrypt(ciphertext, pathname, token, buildId);
        if (servers is not null) return servers;

        // A tag mismatch is what a redeployed player looks like from here.
        logger.XpassBuildIdStale(buildId);

        var refreshed = await buildIds.RefreshAsync(page, cancellationToken);
        if (refreshed is null || refreshed == buildId) return [];

        var underRefreshed = Decrypt(ciphertext, pathname, token, refreshed);
        if (underRefreshed is not null) return underRefreshed;

        // Reported because the empty list below reads downstream as "not in catalogue".
        logger.XpassDecryptUnreadable(refreshed);

        return [];
    }

    private async Task<string> GetCiphertextAsync(string dataUrl, string pageUrl, CancellationToken cancellationToken)
    {
        using var fetch = StartFetch(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{options.PlayerHost}{dataUrl}");
        request.Headers.Referrer = new Uri(pageUrl);
        using var response = await http.SendAsync(request, fetch.Token);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadAsStringAsync(fetch.Token)).Trim();
    }

    private static CancellationTokenSource StartFetch(CancellationToken cancellationToken)
    {
        var fetch = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        fetch.CancelAfter(FetchTimeout);

        return fetch;
    }

    // Mirrors the player: the key is SHA-256 over a build-scoped string of the request's own path
    // and token; the payload is nonce-prefixed, tag-suffixed AES-GCM.
    private static List<XpassServer>? Decrypt(string ciphertext, string pathname, string token, string buildId)
    {
        try
        {
            var key = SHA256.HashData(Encoding.UTF8.GetBytes($"spv3-data-response|{buildId}|{pathname}|{token}"));
            var payload = Base64Url.DecodeFromChars(ciphertext);
            if (payload.Length <= NonceLength + TagLength) return null;

            var nonce = payload.AsSpan(0, NonceLength);
            var body = payload.AsSpan(NonceLength);
            var tag = body[^TagLength..];
            var encrypted = body[..^TagLength];
            var plaintext = new byte[encrypted.Length];

            using var aes = new AesGcm(key, TagLength);
            aes.Decrypt(nonce, encrypted, tag, plaintext);

            return JsonSerializer.Deserialize<List<XpassServer>>(plaintext, JsonSerializerOptions.Web);
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException or FormatException)
        {
            return null;
        }
    }

    [GeneratedRegex(@"var dataUrl=""([^""]+)""")]
    private static partial Regex DataUrlRegex();

    [GeneratedRegex(@"[?&]token=([^&""]+)")]
    private static partial Regex TokenRegex();
}