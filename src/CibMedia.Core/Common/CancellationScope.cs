namespace CibMedia.Core.Common;

// One run at a time: starting the next cancels the one before it, so a slow earlier answer
// cannot land after a fast later one. Linked to the owner's lifetime.
public sealed class CancellationScope(CancellationToken lifetime) : IDisposable
{
    private CancellationTokenSource? _current;

    public void Dispose()
    {
        _current?.Dispose();
    }

    public async Task<CancellationToken> NextAsync()
    {
        var previous = Interlocked.Exchange(
            ref _current,
            CancellationTokenSource.CreateLinkedTokenSource(lifetime));

        if (previous is not null)
        {
            await previous.CancelAsync().ConfigureAwait(false);
            previous.Dispose();
        }

        return _current!.Token;
    }
}
