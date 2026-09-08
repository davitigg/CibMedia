using _Microsoft.Android.Resource.Designer;
using Android.Content;
using Android.Util;
using Android.Views;
using CibMedia.AndroidTv.Design;

namespace CibMedia.AndroidTv.Playback;

// Media3 has no title view of its own, so this is drawn over the player and follows the
// controls. Left at zero alpha rather than Gone after a fade: the view is neither focusable
// nor clickable, and it avoids handing Java an end-action whose managed peer must outlive
// the Activity.
public sealed class PlaybackTitleView : TextView
{
    // Media3's package-private DURATION_FOR_SHOWING_ANIMATION_MS; if a release changes it the
    // title drifts out of step with the controls.
    private const int FadeMs = 250;

    // Tracked rather than read off the view: mid-fade the alpha says nothing about which way
    // it was last sent.
    private bool _shown;

    // Latched because Media3 reports VISIBLE again part-way through a hide.
    private bool _hiding;

    public PlaybackTitleView(Context context, string heading)
        : base(context)
    {
        Text = heading;
        Alpha = 0f;

        SetTextSize(
            ComplexUnitType.Px,
            context.Resources!.GetDimension(ResourceConstant.Dimension.playback_title_text_size));
        SetTextColor(Tokens.TextPrimary(context));
        SetPadding(
            Dimen(context, ResourceConstant.Dimension.playback_title_padding_horizontal),
            Dimen(context, ResourceConstant.Dimension.playback_title_padding_top),
            Dimen(context, ResourceConstant.Dimension.playback_title_padding_horizontal),
            0);
    }

    public static FrameLayout.LayoutParams Placement()
    {
        return new FrameLayout.LayoutParams(
            ViewGroup.LayoutParams.WrapContent,
            ViewGroup.LayoutParams.WrapContent,
            GravityFlags.Top | GravityFlags.Start);
    }

    // The controls hide in two stages, buttons first and the scrubber a second later, and
    // every stage reports VISIBLE with fullyVisible false, so the pair alone cannot separate
    // a show starting from a hide half finished:
    //
    //   show:  NONE -> ANIMATING_SHOW -> ALL_VISIBLE              VISIBLE, VISIBLE
    //   hide:  ALL_VISIBLE -> ANIMATING_HIDE -> ONLY_PROGRESS     VISIBLE, VISIBLE
    //                     -> ANIMATING_HIDE -> NONE               VISIBLE, GONE
    //
    // Once the controls are leaving, every VISIBLE until they are gone belongs to that hide, so
    // the title rides the buttons and leaves the scrubber to finish on its own.
    public void Follow(ViewStates visibility, bool fullyVisible)
    {
        if (visibility != ViewStates.Visible)
        {
            _hiding = false;
            Fade(false);
            return;
        }

        if (fullyVisible)
        {
            _hiding = false;
            Fade(true);
            return;
        }

        if (_shown)
        {
            _hiding = true;
            Fade(false);
            return;
        }

        if (!_hiding) Fade(true);
    }

    private static int Dimen(Context context, int id)
    {
        return context.Resources!.GetDimensionPixelSize(id);
    }

    private void Fade(bool visible)
    {
        _shown = visible;

        Animate()?.Cancel();
        Animate()
            ?.Alpha(visible ? 1f : 0f)
            ?.SetDuration(FadeMs)
            ?.Start();
    }
}
