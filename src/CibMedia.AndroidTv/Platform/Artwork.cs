using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Bumptech.Glide;

namespace CibMedia.AndroidTv.Platform;

// For the details page, which wants a Drawable for the overview row and a Bitmap for the
// background controller rather than an ImageView filled.
public static class Artwork
{
    public static void LoadDrawable(Context context, string? url, int widthPx, int heightPx, Action<Drawable> onReady)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        Glide.With(context)
            .Load(url)
            .Override(widthPx, heightPx)
            .CenterCrop()
            .Into(new DrawableSink(context, onReady));
    }

    public static void LoadBitmap(Context context, string? url, int widthPx, int heightPx, Action<Bitmap> onReady)
    {
        LoadDrawable(
            context,
            url,
            widthPx,
            heightPx,
            drawable =>
            {
                if (ToBitmap(drawable) is { } bitmap) onReady(bitmap);
            });
    }

    // Glide usually hands back a BitmapDrawable, but not always.
    private static Bitmap? ToBitmap(Drawable drawable)
    {
        if (drawable is BitmapDrawable { Bitmap: { } existing }) return existing;

        var width = Math.Max(1, drawable.IntrinsicWidth);
        var height = Math.Max(1, drawable.IntrinsicHeight);

        var bitmap = Bitmap.CreateBitmap(width, height, Bitmap.Config.Argb8888!);
        if (bitmap is null) return null;

        using var canvas = new Canvas(bitmap);
        drawable.SetBounds(0, 0, width, height);
        drawable.Draw(canvas);

        return bitmap;
    }

    // Not one of Glide's own Target types: a C# subclass of a Glide Java class makes the
    // generated callable wrapper import com.bumptech.glide.*, which is not on the classpath
    // that compiles those wrappers. An ImageView is a target Glide already drives, and with
    // Override fixed above it never has to be laid out or attached.
    private sealed class DrawableSink(Context context, Action<Drawable> onReady) : ImageView(context)
    {
        public override void SetImageDrawable(Drawable? drawable)
        {
            base.SetImageDrawable(drawable);

            if (drawable is not null) onReady(drawable);
        }
    }
}
