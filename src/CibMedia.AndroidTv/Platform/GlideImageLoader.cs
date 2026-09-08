using Android.Content;
using Bumptech.Glide;
using CibMedia.Core.Abstractions;

namespace CibMedia.AndroidTv.Platform;

// Glide owns bitmap pooling and per-view request cancellation, which is what keeps a fast
// D-pad scroll from filling a 1 GB box's heap with posters already passed.
public sealed class GlideImageLoader(Context context) : IImageLoader
{
    public void Load(string? url, object target, int widthPx, int heightPx)
    {
        if (target is not ImageView view) return;

        if (string.IsNullOrWhiteSpace(url))
        {
            Cancel(view);
            view.SetImageDrawable(null);
            return;
        }

        Glide.With(context)
            .Load(url)
            .Override(widthPx, heightPx)
            .CenterCrop()
            .Into(view);
    }

    public void Cancel(object target)
    {
        if (target is ImageView view) Glide.With(context).Clear(view);
    }
}
