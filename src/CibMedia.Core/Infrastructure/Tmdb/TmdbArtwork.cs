using System.Text.Json;
using CibMedia.Core.Abstractions;
using TMDbLib.Client;
using TMDbLib.Objects.General;

namespace CibMedia.Core.Infrastructure.Tmdb;

// TMDb hands out bare artwork paths; TMDbClient composes URLs once it holds /configuration.
// This owns which size each slot asks for and keeps that configuration off the network.
public sealed class TmdbArtwork(TmdbClientSource clients, ICacheStore store) : IDisposable
{
    private TMDbClient Client => clients.Client;

    // Sized against dimens.xml at density 2.0. Backdrops and stills stop short of their slot
    // on purpose: TMDb's next size up is "original", which is 3840px and several megabytes.
    private const string CardPoster = "w342";
    private const string HeroBackdrop = "w1280";
    private const string HeroLogo = "w500";
    private const string EpisodeStill = "w300";
    private const string CastPhoto = "w185";

    private const string Key = "tmdb:config";

    // TMDb asks clients to re-check the image host every few days and has moved it before.
    // Every artwork URL is composed from it, so a stale one means no artwork at all.
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    private readonly SemaphoreSlim _gate = new(1, 1);

    public void Dispose()
    {
        _gate.Dispose();
    }

    public async Task EnsureConfigAsync(CancellationToken ct)
    {
        if (Client.HasConfig) return;

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (Client.HasConfig) return;

            if (await CachedAsync(ct).ConfigureAwait(false) is { } cached) Client.SetConfig(cached);
            else await FetchAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public string? Poster(string? path)
    {
        return Compose(path, CardPoster);
    }

    public string? Backdrop(string? path)
    {
        return Compose(path, HeroBackdrop);
    }

    public string? Logo(string? path)
    {
        return Compose(path, HeroLogo);
    }

    public string? Still(string? path)
    {
        return Compose(path, EpisodeStill);
    }

    public string? Profile(string? path)
    {
        return Compose(path, CastPhoto);
    }

    private async Task<TMDbConfig?> CachedAsync(CancellationToken ct)
    {
        try
        {
            // A config without an image host would make every GetImageUrl throw.
            return await store.ReadAsync(Key, Lifetime, ct).ConfigureAwait(false) is { } cached
                   && JsonSerializer.Deserialize(cached, CacheJson.Default.TMDbConfig)
                       is { Images.SecureBaseUrl.Length: > 0 } config
                ? config
                : null;
        }
        catch (Exception error) when (error is JsonException or IOException)
        {
            return null;
        }
    }

    private async Task FetchAsync(CancellationToken ct)
    {
        // Leaves the config on the client as well as returning it.
        var config = await Client.GetConfigAsync().ConfigureAwait(false);

        // Two thirds of the entry's bytes, and nothing here reads them.
        config.ChangeKeys = null;

        try
        {
            await store.WriteAsync(Key, JsonSerializer.SerializeToUtf8Bytes(config, CacheJson.Default.TMDbConfig), ct)
                .ConfigureAwait(false);
        }
        catch (IOException)
        {
        }
    }

    private string? Compose(string? path, string size)
    {
        return string.IsNullOrWhiteSpace(path) || !Client.HasConfig
            ? null
            : Client.GetImageUrl(size, path, true).AbsoluteUri;
    }
}
