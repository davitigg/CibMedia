using AndroidX.Leanback.Widget;
using CibMedia.AndroidTv.Leanback.Support;
using Object = Java.Lang.Object;

namespace CibMedia.AndroidTv.Leanback.Presenters;

// Leanback lays the details header out itself and only asks what goes in the three slots.
public sealed class SynopsisPresenter : AbstractDetailsDescriptionPresenter
{
    protected override void OnBindDescription(ViewHolder? holder, Object? item)
    {
        if (holder is null || JavaRef.Unwrap<Synopsis>(item) is not { } synopsis) return;

        holder.Title!.Text = synopsis.Title;
        holder.Subtitle!.TextFormatted = synopsis.Subtitle;
        holder.Body!.Text = synopsis.Body ?? string.Empty;
    }
}
