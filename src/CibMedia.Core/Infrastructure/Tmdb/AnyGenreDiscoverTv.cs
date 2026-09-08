using TMDbLib.Client;
using TMDbLib.Objects.Discover;

namespace CibMedia.Core.Infrastructure.Tmdb;

// DiscoverTv can only intersect genres: WhereGenresInclude joins ids with ",". Only
// DiscoverMovie ships the "|" union, so for shows it is written straight into the protected
// query bag, without the reflection TrimMode=full would strip.
internal sealed class AnyGenreDiscoverTv(TMDbClient client) : DiscoverTv(client)
{
    public void IncludeWithAnyOfGenre(IEnumerable<int> genreIds)
    {
        Parameters["with_genres"] = string.Join("|", genreIds);
    }
}
