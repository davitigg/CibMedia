using _Microsoft.Android.Resource.Designer;
using Android.Views;
using AndroidX.Media3.UI;

namespace CibMedia.AndroidTv.Playback;

// Media3's bottom bar has no hook for another button and its layout can only be replaced
// whole, which would pin the app to one Media3 release. So a button is inserted into the bar
// at runtime, ahead of whatever is there, sized like the subtitle one and wearing Media3's own
// button style.
public static class ControllerButton
{
    public static ImageButton? AddTo(PlayerView playerView, int iconId, int descriptionId, Action onClick)
    {
        if (playerView.FindViewById<LinearLayout>(ResourceConstant.Id.exo_basic_controls) is not { } bar) return null;
        if (playerView.FindViewById<View>(ResourceConstant.Id.exo_subtitle) is not { } sibling) return null;

        var context = playerView.Context!;

        var button = new ImageButton(context, null, 0, ResourceConstant.Style.ExoStyledControls_Button_Bottom)
        {
            ContentDescription = context.GetString(descriptionId)
        };

        button.SetImageResource(iconId);
        button.Click += (_, _) => onClick();

        bar.AddView(button, 0, new LinearLayout.LayoutParams(sibling.LayoutParameters!));

        return button;
    }
}
