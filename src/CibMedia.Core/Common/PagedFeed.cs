namespace CibMedia.Core.Common;

// Accumulates pages behind one Load<>, so a screen binds a single growing list.
//
// LoadNextAsync hands back the very same instance when it had nothing to add, so a caller can
// tell by reference that nothing changed. Paging is driven off the selection, which asks for
// the next page while one is in flight and after the last has landed.
public sealed class PagedFeed<TItem>(
    Func<int, CancellationToken, Task<Page<TItem>>> fetch,
    int maxPages = PagedFeed.DefaultMaxPages)
{
    // The busy flag and the list are both guarded: a rail is paged from the UI thread while
    // the page's initial load runs on another.
    private readonly Lock _gate = new();
    private readonly List<TItem> _items = [];
    private bool _busy;
    private Load<IReadOnlyList<TItem>> _current = Load.Ready<IReadOnlyList<TItem>>([]);
    private int _loaded;
    private int _totalPages = 1;

    public bool HasMore
    {
        get
        {
            lock (_gate) return _loaded < Math.Min(_totalPages, maxPages);
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _items.Clear();
            _loaded = 0;
            _totalPages = 1;
            _busy = false;
            _current = Load.Ready<IReadOnlyList<TItem>>([]);
        }
    }

    public async Task<Load<IReadOnlyList<TItem>>> LoadNextAsync(CancellationToken ct)
    {
        int next;

        lock (_gate)
        {
            if (_busy || _loaded >= Math.Min(_totalPages, maxPages)) return _current;

            _busy = true;
            next = _loaded + 1;
        }

        try
        {
            var result = await Load.RunAsync(c => fetch(next, c), ct).ConfigureAwait(false);

            lock (_gate)
            {
                // A failed page never empties a rail that already has cards.
                if (result is not Load<Page<TItem>>.Ready ready)
                {
                    if (_items.Count == 0)
                        _current = Load.Failed<IReadOnlyList<TItem>>(
                            ((Load<Page<TItem>>.Failed)result).Error);

                    return _current;
                }

                _items.AddRange(ready.Value.Items);
                _loaded = ready.Value.PageNumber;
                _totalPages = ready.Value.TotalPages;
                _current = Load.Ready<IReadOnlyList<TItem>>([.. _items]);

                return _current;
            }
        }
        finally
        {
            lock (_gate) _busy = false;
        }
    }
}

public static class PagedFeed
{
    // 500 cards. Pages are fetched lazily, so the depth costs nothing until it is used.
    public const int DefaultMaxPages = 25;

    public const int Unlimited = int.MaxValue;
}
