using AndroidX.Leanback.Widget;
using AndroidX.RecyclerView.Widget;

namespace CibMedia.AndroidTv.Leanback.Presenters;

// A ListRowPresenter whose rows take their cards from a pool the caller owns.
//
// The pool is keyed by view type and ItemBridgeAdapter numbers those per row adapter, so a
// pool shared between rows is only safe while all of them draw through the same card
// presenter. A null pool leaves Leanback's own per-row one in place.
public sealed class PooledListRowPresenter(
    int focusZoomFactor,
    bool useFocusDimmer,
    RecyclerView.RecycledViewPool? pool)
    : ListRowPresenter(focusZoomFactor, useFocusDimmer)
{
    protected override void InitializeRowViewHolder(RowPresenter.ViewHolder? vh)
    {
        base.InitializeRowViewHolder(vh);

        if (pool is not null && vh is ViewHolder row && row.GridView is { } grid)
            grid.SetRecycledViewPool(pool);
    }
}
