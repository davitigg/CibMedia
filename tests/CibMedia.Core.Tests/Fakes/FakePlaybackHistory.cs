using CibMedia.Core.Abstractions;
using CibMedia.Core.Catalog;
using CibMedia.Core.Playback;

namespace CibMedia.Core.Tests.Fakes;

public sealed class FakePlaybackHistory : IPlaybackHistory
{
    private readonly List<WatchProgress> _entries = [];

    public event Action? Changed;

    public Task<IReadOnlyList<WatchProgress>> RecentAsync(CancellationToken ct)
    {
        lock (_entries)
        {
            return Task.FromResult<IReadOnlyList<WatchProgress>>(
                [.. _entries.Where(p => !p.IsFinished).OrderByDescending(p => p.WatchedAt)]);
        }
    }

    public Task<WatchProgress?> ForAsync(MediaId id, CancellationToken ct)
    {
        lock (_entries)
        {
            return Task.FromResult(_entries.FirstOrDefault(p => p.Id == id));
        }
    }

    public Task<int> CountAsync(CancellationToken ct)
    {
        lock (_entries)
        {
            return Task.FromResult(_entries.Count);
        }
    }

    public Task RecordAsync(WatchProgress progress, CancellationToken ct)
    {
        lock (_entries)
        {
            _entries.RemoveAll(p => p.Id == progress.Id);
            _entries.Add(progress);
        }

        Changed?.Invoke();

        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken ct)
    {
        lock (_entries)
        {
            _entries.Clear();
        }

        Changed?.Invoke();

        return Task.CompletedTask;
    }
}
