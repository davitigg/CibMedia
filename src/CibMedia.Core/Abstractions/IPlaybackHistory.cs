using CibMedia.Core.Catalog;
using CibMedia.Core.Playback;

namespace CibMedia.Core.Abstractions;

public interface IPlaybackHistory
{
    // Raised once a write has landed, so screens follow the write rather than a lifecycle
    // callback that can run before it.
    event Action? Changed;

    // Unfinished titles only, newest first.
    Task<IReadOnlyList<WatchProgress>> RecentAsync(CancellationToken ct);

    Task RecordAsync(WatchProgress progress, CancellationToken ct);

    Task<WatchProgress?> ForAsync(MediaId id, CancellationToken ct);

    // Everything stored, finished titles included.
    Task<int> CountAsync(CancellationToken ct);

    Task ClearAsync(CancellationToken ct);
}
