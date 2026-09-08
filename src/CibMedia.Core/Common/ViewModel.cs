namespace CibMedia.Core.Common;

public abstract class ViewModel<TState> : IDisposable
    where TState : class
{
    private readonly Lock _gate = new();
    private readonly CancellationTokenSource _lifetime = new();
    private bool _disposed;
    private TState _state;

    protected ViewModel(TState initial)
    {
        _state = initial;
    }

    public TState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    protected CancellationToken Lifetime => _lifetime.Token;

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _lifetime.Cancel();

        OnDispose();

        _lifetime.Dispose();
        StateChanged = null;

        GC.SuppressFinalize(this);
    }

    // No payload on purpose: handlers read State, so a notification that arrives out of order
    // still renders the newest snapshot.
    public event Action? StateChanged;

    // Runs after the lifetime token is cancelled and before it is disposed.
    protected virtual void OnDispose()
    {
    }

    // Atomic read-modify-write: rails complete concurrently. An update that hands back the
    // instance already held is not a change and is not raised as one.
    protected void SetState(Func<TState, TState> update)
    {
        lock (_gate)
        {
            var next = update(_state);

            if (ReferenceEquals(next, _state)) return;

            _state = next;
        }

        StateChanged?.Invoke();
    }
}
