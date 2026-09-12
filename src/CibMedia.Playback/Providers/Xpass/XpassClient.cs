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

    // One budget for every hop this makes rather than one each, so a slow title costs a bounded
    // wait instead of the sum of them. Kept under the client's own timeout so the cut is always
    // this side's to name. The box runs these hops around four times slower than a desktop, which
    // six seconds a hop did not leave room for.
    private static readonly TimeSpan ReadBudget = TimeSpan.FromSeconds(9);

    // The servers the embed offers for a title, in the embed's own order, or empty when the title
    // is not in the catalogue.
    public async Task<IReadOnlyList<XpassServer>> GetServersAsync(string path, CancellationToken cancellationToken)
    {
        using var read = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        read.CancelAfter(ReadBudget);

        try
        {
            return await ReadServersAsync(path, read.Token);
        }
        catch (OperationCanceledException cancellation)
            when (read.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new PlaybackTimeoutException($"the xpass player for {path}", cancellation);
        }
    }

    private async Task<IReadOnlyList<XpassServer>> ReadServersAsync(string path, CancellationToken cancellationToken)
    {
        var pageUrl = $"https://{options.PlayerHost}/e/{path}?autostart=false";

        var page = await http.GetStringAsync(pageUrl, cancellationToken);

        var dataUrl = DataUrlRegex().Match(page).Groups[1].Value;
        if (dataUrl.Length is 0) return [];

        var query = dataUrl.IndexOf('?');
        var pathname = query < 0 ? dataUrl : dataUrl[..query];
        var token = TokenRegex().Match(dataUrl).Groups[1].Value;
        if (token.Length is 0) return [];

        // The build id is a cache read, or on a cold cache the bundle walk; neither depends on the
        // payload, so it rides alongside the download. Outside the budget because it belongs to the
        // stack rather than to this title: a walk cut short here is one the next title repeats.
        var buildIdTask = buildIds.GetAsync(page, CancellationToken.None);
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
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{options.PlayerHost}{dataUrl}");
        request.Headers.Referrer = new Uri(pageUrl);
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();
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