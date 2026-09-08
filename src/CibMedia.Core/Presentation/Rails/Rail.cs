using CibMedia.Core.Catalog;
using CibMedia.Core.Common;

namespace CibMedia.Core.Presentation.Rails;

// The title is deliberately absent: it is a string resource, which belongs to the head.
public sealed record Rail<TKey>(TKey Key, Func<int, CancellationToken, Task<Page<MediaCard>>> Fetch)
    where TKey : notnull;
