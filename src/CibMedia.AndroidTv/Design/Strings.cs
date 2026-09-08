using Android.Content;
using Java.Lang;
using Object = Java.Lang.Object;
using String = Java.Lang.String;

namespace CibMedia.AndroidTv.Design;

public static class Strings
{
    // Android string resources use positional tokens (%1$d), which string.Format does not
    // understand; the Context overload is the one that applies them.
    public static string Format(Context context, int resourceId, params object[] args)
    {
        return context.GetString(resourceId, [.. args.Select(Wrap)]);
    }

    public static string Quantity(Context context, int resourceId, int count)
    {
        return context.Resources!.GetQuantityString(resourceId, count, Integer.ValueOf(count));
    }

    private static Object Wrap(object value)
    {
        return value switch
        {
            int i => Integer.ValueOf(i),
            long l => Long.ValueOf(l),
            _ => new String(value.ToString() ?? string.Empty)
        };
    }
}
