using CibMedia.Core.Abstractions;

namespace CibMedia.Core.Tests.Fakes;

public sealed class FakeCacheStore : ICacheStore
{
    private readonly Dictionary<string, (byte[] Value, DateTimeOffset WrittenAt)> _entries = [];

    public int Writes { get; private set; }

    public Task<byte[]?> ReadAsync(string key, TimeSpan maxAge, CancellationToken ct)
    {
        if (!_entries.TryGetValue(key, out var entry)) return Task.FromResult<byte[]?>(null);

        return Task.FromResult(DateTimeOffset.UtcNow - entry.WrittenAt > maxAge ? null : entry.Value);
    }

    public Task WriteAsync(string key, byte[] value, CancellationToken ct)
    {
        Writes++;
        _entries[key] = (value, DateTimeOffset.UtcNow);

        return Task.CompletedTask;
    }

    public void Age(string key, TimeSpan by)
    {
        if (_entries.TryGetValue(key, out var entry))
            _entries[key] = (entry.Value, entry.WrittenAt - by);
    }

    public Task<long> SizeAsync(CancellationToken ct)
    {
        return Task.FromResult(_entries.Values.Sum(entry => (long)entry.Value.Length));
    }

    public Task ClearAsync(CancellationToken ct)
    {
        _entries.Clear();

        return Task.CompletedTask;
    }
}
