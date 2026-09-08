using CibMedia.Core.Catalog;
using CibMedia.Core.Common;

namespace CibMedia.Core.Presentation.Rails;

// A page that is a list of rails. Home and the two catalogue sections differ only in which
// rails they declare.
public abstract class RailsViewModel<TKey> : ViewModel<RailsState<TKey>>
    where TKey : notnull
{
    private const int MaxConcurrentLoads = 4;

    private readonly Dictionary<TKey, PagedFeed<MediaCard>> _feeds;
    private readonly TKey[] _order;
    private bool _loaded;
    private Task? _loading;

    protected RailsViewModel(IReadOnlyList<Rail<TKey>> rails)
        : base(RailsState<TKey>.Loading(rails.Select(rail => rail.Key)))
    {
        _order = [.. rails.Select(rail => rail.Key)];
        _feeds = rails.ToDictionary(rail => rail.Key, rail => new PagedFeed<MediaCard>(rail.Fetch));
    }

    // Top to bottom; the screen reads its rows off this rather than declaring the list again.
    public IReadOnlyList<TKey> Order => _order;

    // The view model outlives its fragment, so a section switch does not re-fetch a page that
    // is already filled. A page settles once every rail came back; until then a return to the
    // section asks again for the rails that failed, and only for those.
    public Task EnsureLoadedAsync()
    {
        if (_loaded) return Task.CompletedTask;

        if (_loading is { IsCompleted: false } running) return running;

        return _loading = LoadOnceAsync();
    }

    private async Task LoadOnceAsync()
    {
        var failed = _order.Where(HasFailed).ToArray();

        await (failed.Length > 0
                ? Task.WhenAll(failed.Select(ReloadAsync))
                : LoadAsync())
            .ConfigureAwait(false);

        _loaded = !_order.Any(HasFailed);
    }

    private bool HasFailed(TKey key)
    {
        return State.For(key) is Load<IReadOnlyList<MediaCard>>.Failed;
    }

    // Bounded and in declaration order, so the rails on screen resolve first and a page fills
    // without landing every row on the UI thread in one burst.
    public async Task LoadAsync()
    {
        using var gate = new SemaphoreSlim(MaxConcurrentLoads);

        await Task.WhenAll(_order.Select(Load)).ConfigureAwait(false);

        async Task Load(TKey key)
        {
            try
            {
                await gate.WaitAsync(Lifetime).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                await LoadMoreAsync(key).ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        }
    }

    public bool HasMore(TKey key)
    {
        return _feeds.TryGetValue(key, out var feed) && feed.HasMore;
    }

    public async Task LoadMoreAsync(TKey key)
    {
        if (!_feeds.TryGetValue(key, out var feed) || !feed.HasMore) return;

        var result = await feed.LoadNextAsync(Lifetime).ConfigureAwait(false);
        SetState(state => state.With(key, result));
    }

    // Drops what a rail holds and asks for its first page again.
    public Task ReloadAsync(TKey key)
    {
        if (!_feeds.TryGetValue(key, out var feed)) return Task.CompletedTask;

        feed.Reset();
        return LoadMoreAsync(key);
    }
}
