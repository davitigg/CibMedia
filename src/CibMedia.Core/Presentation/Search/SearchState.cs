using CibMedia.Core.Catalog;
using CibMedia.Core.Common;

namespace CibMedia.Core.Presentation.Search;

public sealed record SearchState(string Query, Load<IReadOnlyList<MediaCard>> Results)
{
    public static SearchState Initial { get; } = new(string.Empty, Load.Ready<IReadOnlyList<MediaCard>>([]));
}
