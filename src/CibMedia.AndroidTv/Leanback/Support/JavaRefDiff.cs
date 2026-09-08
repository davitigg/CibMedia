using AndroidX.Leanback.Widget;
using Object = Java.Lang.Object;

namespace CibMedia.AndroidTv.Leanback.Support;

// Without a diff callback ArrayObjectAdapter.SetItems falls back to notifyChanged(), which
// rebinds every card in the row and drops the focused one.
public sealed class JavaRefDiff<TItem>(Func<TItem, object> identity) : DiffCallback
    where TItem : class
{
    public override bool AreItemsTheSame(Object? left, Object? right)
    {
        return Both(left, right, (a, b) => identity(a).Equals(identity(b)));
    }

    public override bool AreContentsTheSame(Object? left, Object? right)
    {
        return Both(left, right, (a, b) => EqualityComparer<TItem>.Default.Equals(a, b));
    }

    private static bool Both(Object? left, Object? right, Func<TItem, TItem, bool> compare)
    {
        return JavaRef.Unwrap<TItem>(left) is { } a && JavaRef.Unwrap<TItem>(right) is { } b && compare(a, b);
    }
}
