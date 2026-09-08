using _Microsoft.Android.Resource.Designer;
using Android.Views;
using AndroidX.Media3.UI;
using AndroidX.RecyclerView.Widget;

namespace CibMedia.AndroidTv.Playback;

// The player's pick list, drawn the way Media3 draws the gear's own sub-menus: its list layout
// and rows, a check on the current entry, the same popup placed the same way. Media3 keeps
// the adapters behind that menu private, so the rows are filled here. Null for a dismissal.
public static class ControllerMenu
{
    private static PopupWindow? _open;

    public static Task<int?> ChooseAsync(PlayerView playerView, View button, IReadOnlyList<string> entries, int current)
    {
        var context = playerView.Context!;
        var list = (RecyclerView)LayoutInflater.From(context)!.Inflate(ResourceConstant.Layout.exo_styled_settings_list, null)!;
        var chosen = new TaskCompletionSource<int?>();
        var window = new PopupWindow(list, ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent, true);

        list.SetLayoutManager(new LinearLayoutManager(context));
        list.SetAdapter(new Entries(entries, current, index =>
        {
            chosen.TrySetResult(index);
            window.Dismiss();
        }));

        // Sized and placed as PlayerControlView does for the gear: the content, capped to the
        // view less a margin each side, then dropped from the button that asked with offsets
        // that Android clamps to the screen, which is what puts the gear's menu flush right.
        var margin = context.Resources!.GetDimensionPixelSize(ResourceConstant.Dimension.exo_settings_offset);
        list.Measure(0, 0);
        window.Width = Math.Min(list.MeasuredWidth, playerView.Width - 2 * margin);
        window.Height = Math.Min(list.MeasuredHeight, playerView.Height - 2 * margin);

        // The controller stays up while the menu is open, as it does behind the gear.
        var timeout = playerView.ControllerShowTimeoutMs;
        playerView.ControllerShowTimeoutMs = 0;
        playerView.ShowController();

        window.DismissEvent += (_, _) =>
        {
            if (ReferenceEquals(_open, window)) _open = null;

            chosen.TrySetResult(null);
            playerView.ControllerShowTimeoutMs = timeout;
            playerView.ShowController();
        };

        _open?.Dismiss();
        _open = window;

        window.ShowAsDropDown(button, playerView.Width - window.Width - margin, -window.Height - margin);
        list.Post(() =>
            (list.FindViewHolderForAdapterPosition(Math.Max(current, 0))?.ItemView ?? list.GetChildAt(0))?.RequestFocus());

        return chosen.Task;
    }

    // A list describes the stream that was playing when it opened. The player calls this the
    // moment that stops being true, so a check never points at something that no longer plays.
    public static void Dismiss()
    {
        _open?.Dismiss();
    }

    private sealed class Entries(IReadOnlyList<string> entries, int current, Action<int> onPick) : RecyclerView.Adapter
    {
        public override int ItemCount => entries.Count;

        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            var view = LayoutInflater.From(parent.Context)!
                .Inflate(ResourceConstant.Layout.exo_styled_sub_settings_list_item, parent, false)!;

            return new Row(view, onPick);
        }

        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            var row = (Row)holder;

            row.Text.Text = entries[position];
            row.Check.Visibility = position == current ? ViewStates.Visible : ViewStates.Invisible;
        }
    }

    private sealed class Row : RecyclerView.ViewHolder
    {
        public Row(View view, Action<int> onPick) : base(view)
        {
            Text = view.FindViewById<TextView>(ResourceConstant.Id.exo_text)!;
            Check = view.FindViewById<ImageView>(ResourceConstant.Id.exo_check)!;

            view.Focusable = true;
            view.Click += (_, _) => onPick(AbsoluteAdapterPosition);
        }

        public TextView Text { get; }

        public ImageView Check { get; }
    }
}
