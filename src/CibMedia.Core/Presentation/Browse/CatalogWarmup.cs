using CibMedia.Core.Abstractions;
using CibMedia.Core.Catalog;
using CibMedia.Core.Common;

namespace CibMedia.Core.Presentation.Browse;

// A section's view model is not built until the section is opened, so on a cold cache the
// first visit waited on the network. This fetches the rail each section opens on while Home
// is painting, filling the cache rather than any view model. Only the first page: the rest
// is below the fold and paged in behind it.
public sealed class CatalogWarmup(ICatalog catalog)
{
    public Task RunAsync(CancellationToken ct)
    {
        return Task.WhenAll(WarmAsync(MediaKind.Movie, ct), WarmAsync(MediaKind.TvShow, ct));
    }

    // Through Load.RunAsync for its error handling: a warm-up that cannot reach the network
    // is not a failure.
    private Task<Load<Page<MediaCard>>> WarmAsync(MediaKind kind, CancellationToken ct)
    {
        var opening = BrowseRails.For(catalog, kind)[0];

        return Load.RunAsync(c => opening.Fetch(1, c), ct);
    }
}
