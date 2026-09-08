using Android.Content;
using Android.Widget;

namespace CibMedia.AndroidTv.Design;

// Built on the application context: an Activity's context carries the Display size density
// override, and a toast should look like every other toast on the box.
public static class Toasts
{
    public static void Show(Context context, int messageId, ToastLength length)
    {
        Toast.MakeText(context.ApplicationContext, messageId, length)?.Show();
    }

    public static void Show(Context context, string message, ToastLength length)
    {
        Toast.MakeText(context.ApplicationContext, message, length)?.Show();
    }
}
