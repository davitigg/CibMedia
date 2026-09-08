using CibMedia.Core.Abstractions;
using CibMedia.Core.Catalog;
using CibMedia.Core.Common;
using CibMedia.Core.Playback;

namespace CibMedia.Core.Presentation.Remote;

// Whether the catalogue has the title a phone named, asked before the box opens a page for it:
// any number fits in the body, and a page that opens on nothing says less than a toast does.
// Nothing is paid for the check twice — the page's own fetch reads the answer back from the cache.
public static class TitleCheck
{
    // Null when the catalogue has it. The error is carried rather than reduced to a flag: an id
    // TMDb has never heard of and a TMDb nobody could reach are different things to be told.
    public static async Task<AppError?> MissingAsync(
        ICatalog catalog, TitleTarget target, CancellationToken ct)
    {
        if (target.Id.Kind is MediaKind.Movie)
        {
            return await Load.RunAsync(c => catalog.GetMovieAsync(target.Id.TmdbId, c), ct)
                    .ConfigureAwait(false)
                is Load<MovieDetails>.Failed movie
                ? movie.Error
                : null;
        }

        return await Load.RunAsync(c => catalog.GetTvShowAsync(target.Id.TmdbId, c), ct).ConfigureAwait(false)
            is Load<TvShowDetails>.Failed show
            ? show.Error
            : null;
    }
}
