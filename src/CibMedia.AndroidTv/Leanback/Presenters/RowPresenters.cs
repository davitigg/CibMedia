using AndroidX.Leanback.Widget;
using AndroidX.RecyclerView.Widget;

namespace CibMedia.AndroidTv.Leanback.Presenters;

// One place to say how a row behaves. Focus is Leanback's own zoom at its largest step,
// which scales about the card's centre without reflowing the row; the dimmer washes a dark
// poster wall out, and a drop shadow only muddies the edge on a flat near-black surface.
public static class RowPresenters
{
    public static ListRowPresenter Standard()
    {
        return Styled(null);
    }

    // For the rails sections, which share one pool: re-inflating a section's visible cards on
    // every switch measured at 230-280ms on a Mi Box, against 17ms for an empty section.
    public static ListRowPresenter Pooled(RecyclerView.RecycledViewPool pool)
    {
        return Styled(pool);
    }

    private static PooledListRowPresenter Styled(RecyclerView.RecycledViewPool? pool)
    {
        return new PooledListRowPresenter(FocusHighlight.ZoomFactorLarge, false, pool)
        {
            ShadowEnabled = false,
            SelectEffectEnabled = false
        };
    }
}
