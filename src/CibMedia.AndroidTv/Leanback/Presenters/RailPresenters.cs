using AndroidX.Leanback.Widget;
using AndroidX.RecyclerView.Widget;
using CibMedia.Core.Abstractions;

namespace CibMedia.AndroidTv.Leanback.Presenters;

// What every rails section draws through, and the pool its cards come from. Held outside the
// fragments because Leanback rebuilds a section's fragment on every switch. One card presenter
// for all of them is what makes the shared pool safe.
public sealed class RailPresenters : IDisposable
{
    // Leanback rows hold five of a type by default; a page shows roughly thirty cards at once.
    private const int PooledCards = 48;

    private readonly RecyclerView.RecycledViewPool _pool = new();

    public RailPresenters(IImageLoader images)
    {
        _pool.SetMaxRecycledViews(0, PooledCards);

        Cards = new MediaCardPresenter(images);
        ListRows = RowPresenters.Pooled(_pool);
    }

    public MediaCardPresenter Cards { get; }

    public ListRowPresenter ListRows { get; }

    // A pooled card holds the Activity that inflated it, so the pool is dropped with the shell.
    public void Clear()
    {
        _pool.Clear();
        Cards.Forget();
    }

    public void Dispose()
    {
        _pool.Dispose();
    }
}
