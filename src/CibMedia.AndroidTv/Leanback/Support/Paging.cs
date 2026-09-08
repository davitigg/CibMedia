using AndroidX.Leanback.Widget;
using Object = Java.Lang.Object;

namespace CibMedia.AndroidTv.Leanback.Support;

// Off the selected position rather than a scroll offset: Leanback reports the adapter position
// that holds focus, so this is near the end of what the user has reached.
public static class Paging
{
    public static bool NearEnd(ObjectAdapter? items, Object? item, int threshold)
    {
        if (items is not ArrayObjectAdapter adapter) return false;

        var position = adapter.IndexOf(item);

        return position >= 0 && position >= adapter.Size() - threshold;
    }
}
