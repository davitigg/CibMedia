using Android.Views;
using AndroidX.Media3.UI;

namespace CibMedia.AndroidTv.Playback;

public sealed class ControllerVisibilityListener(Action<ViewStates> onChanged)
    : Java.Lang.Object, PlayerView.IControllerVisibilityListener
{
    public void OnVisibilityChanged(int visibility)
    {
        onChanged((ViewStates)visibility);
    }
}
