using Android.Content;
using Android.Views;
using AndroidX.Leanback.Widget;

namespace CibMedia.AndroidTv.Leanback.Support;

// Lifts the title of the rail that holds focus to full strength. Leanback holds every row
// header at lb_browse_header_unselect_alpha and only brightens the section list's own.
//
// Posted rather than set inline: Leanback re-applies the alpha in the select-level pass that
// follows this callback.
public sealed class RowHeaderHighlight
{
    private readonly float _dimmed;
    private View? _selected;

    public RowHeaderHighlight(Context context)
    {
        var resources = context.Resources!;
        var dimmed = resources.GetIdentifier("lb_browse_header_unselect_alpha", "fraction", context.PackageName);

        _dimmed = dimmed != 0 ? resources.GetFraction(dimmed, 1, 1) : 0.5f;
    }

    public void OnSelected(object? sender, BaseOnItemViewSelectedEventArgs e)
    {
        var header = e.RowViewHolder?.HeaderViewHolder?.View;

        if (ReferenceEquals(header, _selected)) return;

        var previous = _selected;
        _selected = header;

        previous?.Post(() => previous.Alpha = _dimmed);
        header?.Post(() => header.Alpha = 1f);
    }
}
