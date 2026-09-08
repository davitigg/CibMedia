using CibMedia.Core.Abstractions;
using TMDbLib.Client;

namespace CibMedia.Core.Infrastructure.Tmdb;

// TMDbClient takes its key at construction and has no setter for it, so a key changed in
// Settings means a new client. Every TMDb call goes through here so that swap happens once.
public sealed class TmdbClientSource(IApiKeys keys) : IDisposable
{
    private readonly Lock _gate = new();

    private volatile Session? _session;

    // Lock-free on the ordinary path: composing artwork URLs asks for this five times a card.
    // The key and its client live in one object behind one volatile field, so a reader either
    // sees a matched pair or falls through to the lock.
    public TMDbClient Client
    {
        get
        {
            var key = keys[ApiKeyKind.Tmdb]
                      ?? throw new InvalidOperationException("No TMDb API key has been set.");

            if (_session is { } cached && cached.Key == key) return cached.Client;

            lock (_gate)
            {
                if (_session is { } current && current.Key == key) return current.Client;

                _session?.Client.Dispose();
                _session = new Session(
                    key,
                    new TMDbClient(key)
                    {
                        DefaultLanguage = "en-US",
                        // "en,null" keeps language-neutral artwork, which is most backdrops
                        // and logos; "en" alone would drop them.
                        DefaultImageLanguage = "en,null"
                    });

                return _session.Client;
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _session?.Client.Dispose();
            _session = null;
        }
    }

    private sealed record Session(string Key, TMDbClient Client);
}
