using AndroidX.Media3.Common;
using AndroidX.Media3.Common.Text;

namespace CibMedia.AndroidTv.Playback;

// Every member is implemented explicitly. Player.Listener's methods are all default in Java,
// but the binding's callable wrapper does not carry that through: OnEvents and
// OnSurfaceSizeChanged threw AbstractMethodError when left unoverridden, killing the process.
public sealed class PlayerEventListener(
    Action onEnded,
    Action<PlaybackException?> onError,
    Action<Tracks?> onTracks
) : Java.Lang.Object, IPlayerListener
{
    // Player.STATE_ENDED, a Java @IntDef the binding carries no constant for.
    private const int StateEnded = 4;

    public void OnPlaybackStateChanged(int playbackState)
    {
        if (playbackState == StateEnded) onEnded();
    }

    public void OnPlayerError(PlaybackException? error)
    {
        onError(error);
    }

    // Also fires with null when an error is cleared, and alongside OnPlayerError.
    public void OnPlayerErrorChanged(PlaybackException? error)
    {
    }

    public void OnAudioAttributesChanged(AudioAttributes? audioAttributes)
    {
    }

    public void OnAudioSessionIdChanged(int audioSessionId)
    {
    }

    public void OnAvailableCommandsChanged(PlayerCommands? availableCommands)
    {
    }

    public void OnCues(CueGroup? cueGroup)
    {
    }

    public void OnCuesDeprecated(IList<Cue>? cues)
    {
    }

    public void OnDeviceInfoChanged(DeviceInfo? deviceInfo)
    {
    }

    public void OnDeviceVolumeChanged(int volume, bool muted)
    {
    }

    public void OnEvents(IPlayer? player, PlayerEvents? events)
    {
    }

    public void OnIsLoadingChanged(bool isLoading)
    {
    }

    public void OnIsPlayingChanged(bool isPlaying)
    {
    }

    public void OnLoadingChanged(bool isLoading)
    {
    }

    public void OnMaxSeekToPreviousPositionChanged(long maxSeekToPreviousPositionMs)
    {
    }

    public void OnMediaItemTransition(MediaItem? mediaItem, int reason)
    {
    }

    public void OnMediaMetadataChanged(MediaMetadata? mediaMetadata)
    {
    }

    public void OnMetadata(Metadata? metadata)
    {
    }

    public void OnPlayWhenReadyChanged(bool playWhenReady, int reason)
    {
    }

    public void OnPlaybackParametersChanged(PlaybackParameters? playbackParameters)
    {
    }

    public void OnPlaybackSuppressionReasonChanged(int playbackSuppressionReason)
    {
    }

    public void OnPlayerStateChanged(bool playWhenReady, int playbackState)
    {
    }

    public void OnPlaylistMetadataChanged(MediaMetadata? mediaMetadata)
    {
    }

    public void OnPositionDiscontinuity(PlayerPositionInfo? oldPosition, PlayerPositionInfo? newPosition, int reason)
    {
    }

    public void OnRenderedFirstFrame()
    {
    }

    public void OnRepeatModeChanged(int repeatMode)
    {
    }

    public void OnSeekBackIncrementChanged(long seekBackIncrementMs)
    {
    }

    public void OnSeekForwardIncrementChanged(long seekForwardIncrementMs)
    {
    }

    public void OnShuffleModeEnabledChanged(bool shuffleModeEnabled)
    {
    }

    public void OnSkipSilenceEnabledChanged(bool skipSilenceEnabled)
    {
    }

    public void OnSurfaceSizeChanged(int width, int height)
    {
    }

    public void OnTimelineChanged(Timeline? timeline, int reason)
    {
    }

    public void OnTrackSelectionParametersChanged(TrackSelectionParameters? parameters)
    {
    }

    // The first time a player knows its tracks is the first chance to pick one by name.
    public void OnTracksChanged(Tracks? tracks)
    {
        onTracks(tracks);
    }

    public void OnVideoSizeChanged(VideoSize? videoSize)
    {
    }

    public void OnVolumeChanged(float volume)
    {
    }
}
