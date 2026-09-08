namespace CibMedia.Core.Abstractions;

// Bytes in, bytes out. What to cache and for how long is the caller's policy.
public interface ICacheStore
{
    Task<byte[]?> ReadAsync(string key, TimeSpan maxAge, CancellationToken ct);

    Task WriteAsync(string key, byte[] value, CancellationToken ct);

    // Reported, never enforced: nothing prunes on this number.
    Task<long> SizeAsync(CancellationToken ct);

    Task ClearAsync(CancellationToken ct);
}
