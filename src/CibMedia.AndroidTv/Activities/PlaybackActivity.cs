using _Microsoft.Android.Resource.Designer;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Util;
using Android.Views;
using AndroidX.Media3.Common;
using AndroidX.Media3.DataSource;
using AndroidX.Media3.ExoPlayer;
using AndroidX.Media3.ExoPlayer.Source;
using AndroidX.Media3.UI;
using Bumptech.Glide;
using CibMedia.Core.Abstractions;
using CibMedia.Core.Common;
using CibMedia.Core.Playback;
using CibMedia.Core.Presentation.Playback;
using CibMedia.AndroidTv.App;
using CibMedia.AndroidTv.Design;
using CibMedia.AndroidTv.Platform;
using CibMedia.AndroidTv.Playback;
using Microsoft.Extensions.DependencyInjection;

namespace CibMedia.AndroidTv.Activities;

// Its own Activity: playback needs its own window flags, theme and back-stack behaviour.
[Activity(
    Theme = "@style/Theme.CibMedia.Playback",
    LaunchMode = LaunchMode.SingleTask,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.KeyboardHidden)]
public sealed class PlaybackActivity : Activity
{
    // What run.ps1 filters logcat on.
    private const string LogTag = "CibMedia";

    private static readonly TimeSpan AutoCloseDelay = TimeSpan.FromSeconds(3);

    // Below this a session says nothing about where the user is in a title: a brief accidental
    // stop must not overwrite real progress, and seeking to the end must not roll it forward.
    private static readonly TimeSpan MinimumSessionForProgress = TimeSpan.FromMinutes(2);

    private PlaybackArgs? _args;
    private IPlaybackPreferences? _preferences;

    // The dubs to start in, best first, shared by the progressive pick and ExoPlayer's own
    // choice within an HLS stream.
    private IReadOnlyList<string> _languages = [];
    private bool _onScreen;
    private DateTimeOffset? _playbackStartedAt;
    private IExoPlayer? _player;
    private PlayerView? _playerView;
    private bool _progressSaved;
    private ImageButton? _providerButton;
    private ImageButton? _streamsButton;

    // Kept so returning from the background replays it rather than resolving again.
    private PlayableStream? _stream;

    // Labels that failed this sitting, so a fallback tries each of a provider's streams once.
    private readonly HashSet<string> _failed = new(StringComparer.OrdinalIgnoreCase);

    // A remote command can arrive while the one before it is still resolving.
    private readonly CancellationScope _resolving = new(CancellationToken.None);

    private PlaybackTitleView? _titleView;

    protected override void AttachBaseContext(Context? @base)
    {
        base.AttachBaseContext(ScaledContext.Wrap(@base));
    }

    // Back closes the controls first and the player only once they are closed.
    public override void OnBackPressed()
    {
        if (_playerView?.IsControllerFullyVisible == true)
        {
            _playerView.HideController();
            return;
        }

        Close();
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        AppServices.Initialise(this);
        Window?.AddFlags(WindowManagerFlags.KeepScreenOn);

        // A browsed session leaves ~90MB of posters in Glide's native cache, which the decoder
        // then competes with on a 1GB box. Nothing here is a poster.
        Glide.Get(this).ClearMemory();

        _args = PlaybackArgs.ReadFrom(Intent);

        if (_args is null)
        {
            Finish();
            return;
        }

        _preferences = AppServices.Provider.GetRequiredService<IPlaybackPreferences>();
        _languages = LanguageOrder.Of(_preferences.PreferredLanguage, DeviceLanguages.Codes(this));

        SetContentView(BuildContent(_args));

        _ = StartAsync(_args);
    }

    // SingleTask, so a second command while the player is up arrives here rather than as another
    // Activity: without this the Intent is dropped and the box goes on playing what it already
    // had. Nothing is torn down here — StartAsync commits only once it has a stream to switch to.
    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);

        if (PlaybackArgs.ReadFrom(intent) is not { } args) return;

        Intent = intent;

        _ = StartAsync(args);
    }

    // The player is held only while on screen: these boxes have one hardware decoder between
    // every app.
    protected override void OnStart()
    {
        base.OnStart();

        _onScreen = true;
        StartPlayerIfReady();
    }

    protected override void OnStop()
    {
        _onScreen = false;

        SaveProgress();
        ReleasePlayer();

        base.OnStop();
    }

    protected override void OnDestroy()
    {
        _resolving.Dispose();

        base.OnDestroy();
    }

    private FrameLayout BuildContent(PlaybackArgs args)
    {
        _playerView = new PlayerView(this);
        _playerView.SetShowSubtitleButton(true);

        // Each is inserted at the head of the bar, so Streams, added second, sits left of Provider.
        _providerButton = ControllerButton.AddTo(
            _playerView,
            ResourceConstant.Drawable.ic_provider,
            ResourceConstant.String.action_provider,
            () => _ = ChangeProviderAsync());
        _streamsButton = ControllerButton.AddTo(
            _playerView,
            ResourceConstant.Drawable.ic_playlist_play,
            ResourceConstant.String.action_streams,
            () => _ = ChangeStreamAsync());

        // Media3's buffering spinner defaults to never shown, and only the show_buffering XML
        // attribute moves it, which a PlayerView built in code cannot carry.
        _playerView.SetShowBuffering(PlayerView.ShowBufferingWhenPlaying);

        // Black rather than the app background: letterbox bars are screen edge, and the shutter
        // matches so nothing about the frame changes as playback starts or stops.
        _playerView.SetBackgroundColor(Color.Black);
        _playerView.SetShutterBackgroundColor(Color.Black);

        _titleView = new PlaybackTitleView(this, args.Heading);
        _playerView.SetControllerVisibilityListener(
            new ControllerVisibilityListener(
                visibility => _titleView.Follow(visibility, _playerView.IsControllerFullyVisible)));

        var root = new FrameLayout(this);
        root.AddView(
            _playerView,
            new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.MatchParent));
        root.AddView(_titleView, PlaybackTitleView.Placement());

        return root;
    }

    // Resolves before it commits, so a command that turns out to have nothing behind it costs
    // the viewer nothing: whatever is playing is only torn down once there is a stream to put
    // in its place.
    private async Task StartAsync(PlaybackArgs args)
    {
        var resolving = await _resolving.NextAsync().ConfigureAwait(true);

        Load<PlayableStream> resolved;
        try
        {
            resolved = await Load
                .RunAsync(
                    ct => AppServices.Provider.GetRequiredService<IStreamProvider>()
                        .ResolveAsync(args.Target, ct),
                    resolving)
                .ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        // Checked rather than left to the token: a superseded resolve runs to completion and would
        // otherwise start the stream the viewer just replaced.
        if (resolving.IsCancellationRequested) return;

        if (resolved is not Load<PlayableStream>.Ready { Value: { } stream })
        {
            Refuse((resolved as Load<PlayableStream>.Failed)?.Error);
            return;
        }

        SaveProgress();
        ReleasePlayer();

        _args = args;
        _playbackStartedAt = null;
        _progressSaved = false;
        _failed.Clear();

        if (_titleView is not null) _titleView.Text = args.Heading;

        // The details page's choice, else the Settings default, else the first the API offered.
        var provider = args.Provider ?? _preferences?.DefaultProvider;

        SetStream(
            stream.ChooseProvider(provider).PreferLanguages(_languages)
                with { ResumePositionMs = args.ResumePositionMs });

        StartPlayerIfReady();
    }

    private void Refuse(AppError? error)
    {
        var message = error?.Kind switch
        {
            AppErrorKind.NotFound => ResourceConstant.String.playback_not_available,
            AppErrorKind.Rejected => ResourceConstant.String.playback_rejected,
            AppErrorKind.Server => ResourceConstant.String.playback_server,
            AppErrorKind.Offline => ResourceConstant.String.playback_offline,
            AppErrorKind.Timeout => ResourceConstant.String.playback_timeout,
            _ => ResourceConstant.String.playback_failed
        };

        Toasts.Show(this, message, ToastLength.Long);

        // A refused switch leaves the running stream alone: only a first command with nothing
        // behind it has an empty screen to close.
        if (_player is null) Finish();
    }

    // Every place that replaces the stream goes through here. A list still open was about the
    // stream before this one: a fallback can land while it shows.
    private void SetStream(PlayableStream stream)
    {
        _stream = stream;

        ControllerMenu.Dismiss();
    }

    // Both lists open even with one entry: a button that comes and goes reads as a fault.
    private async Task ChangeProviderAsync()
    {
        if (_stream is not { } stream || _playerView is null || _providerButton is null) return;

        var names = stream.ProviderNames;
        var current = stream.ProviderIndex;

        var picked = await ControllerMenu.ChooseAsync(_playerView, _providerButton, names, current).ConfigureAwait(true);

        if (picked is not { } index || index == current) return;

        // The tried labels belong to the provider being left; another provider's streams have
        // earned none of them.
        _failed.Clear();

        Replace(stream.ChooseProvider(names[index]).PreferLanguages(_languages));
    }

    // Every stream of the provider by the API's name for it: a dub and quality, a server, a channel.
    private async Task ChangeStreamAsync()
    {
        if (_stream is not { } stream || _playerView is null || _streamsButton is null) return;

        var labels = stream.Source.Streams.Select(entry => entry.Label).ToList();
        var current = stream.StreamIndex;

        var picked = await ControllerMenu.ChooseAsync(_playerView, _streamsButton, labels, current).ConfigureAwait(true);

        if (picked is { } index && index != current) Replace(stream.ChooseStream(labels[index]));
    }

    // ExoPlayer has no notion of the same title from another file, so a switch is a new media
    // item started where the old one was. Progress is saved first: the session clock restarts
    // with the player, and a long sitting must not be lost to a switch. The view gets the new
    // player before the old one is released: clearing it first hides the controls, and Media3
    // moves focus to Play whenever it shows them again.
    private void Replace(PlayableStream next)
    {
        if (IsFinishing) return;

        SaveProgress();

        var previous = Detach();

        SetStream(next with { ResumePositionMs = _stream?.ResumePositionMs ?? next.ResumePositionMs });
        StartPlayerIfReady();

        previous?.Release();
    }

    // Resolving and coming on screen arrive in either order; whichever lands second starts it.
    private void StartPlayerIfReady()
    {
        if (!_onScreen || _player is not null || _stream is null) return;

        StartPlayer(_stream);
    }

    private void StartPlayer(PlayableStream stream)
    {
        // The descriptor's headers are authoritative: several CDNs 403 ExoPlayer's default
        // user-agent. Accept is added because ExoPlayer's data source sends none, and at least one
        // CDN treats its absence as a bot signal and serves a bogus 200 in place of the manifest.
        var entry = stream.Current;
        var headers = new Dictionary<string, string>(stream.Source.Headers);
        headers.TryAdd("Accept", "*/*");

        var dataSource = new DefaultHttpDataSource.Factory().SetDefaultRequestProperties(headers);

        _player = new ExoPlayerBuilder(this)
            .SetMediaSourceFactory(new DefaultMediaSourceFactory(dataSource))!
            .Build();

        _player!.AddListener(new PlayerEventListener(OnPlaybackEnded, OnPlaybackFailed));

        // The order the progressive pick used, so an HLS stream's Auto lands on the same dub
        // when its playlist has it. Subtitles wait to be picked from the CC menu: a playlist can
        // carry its own text track flagged default, and ExoPlayer would show it unasked.
        _player.TrackSelectionParameters = _player.TrackSelectionParameters!
            .BuildUpon()!
            .SetPreferredAudioLanguages([.. _languages])!
            .SetTrackTypeDisabled(C.TrackTypeText, true)!
            .Build()!;

        _playerView!.Player = _player;

        _player.SetMediaItem(
            new MediaItem.Builder()
                .SetUri(entry.Url)!
                .SetMimeType(entry.IsHls ? MimeTypes.ApplicationM3u8 : null)!
                .SetSubtitleConfigurations(SubtitleTracks.For(stream.Source.Subtitles))!
                .Build());

        if (stream.ResumePositionMs > 0) _player.SeekTo(stream.ResumePositionMs);

        _player.Prepare();
        _player.PlayWhenReady = true;

        _progressSaved = false;
        _playbackStartedAt = DateTimeOffset.UtcNow;
    }

    // Saved before the player is released: OnStop runs after Finish, and by then there is no
    // player to read the position off. SaveProgress is guarded against running twice.
    private void Close()
    {
        SaveProgress();

        ReleasePlayer();
        Finish();
    }

    // Saving starts here rather than in OnStop: a finished episode's save costs a catalog
    // round-trip to find the next one, and OnStop races the details page underneath coming
    // back into view.
    private async void OnPlaybackEnded()
    {
        SaveProgress();

        await Task.Delay(AutoCloseDelay).ConfigureAwait(true);

        if (!IsFinishing) Close();
    }

    // ExoPlayer stops on an error and stays stopped on a frozen frame.
    private async void OnPlaybackFailed(PlaybackException? error)
    {
        Log.Error(LogTag, $"Playback failed: {error?.ErrorCodeName} ({error?.ErrorCode}) {error?.Message}");

        // Off ExoPlayer's own callback before the player is touched: it is still dispatching to
        // its listeners, and both paths below release it.
        await Task.Yield();

        if (IsFinishing) return;

        // The provider's other streams, each once, before anything is said.
        if (LooksResolvable(error) && _stream is { } playing)
        {
            _failed.Add(playing.Current.Label);

            if (playing.NextFallback(_failed) is { } fallback)
            {
                Replace(fallback);
                Toasts.Show(
                    this,
                    Strings.Format(this, ResourceConstant.String.playback_switched, fallback.Current.Label),
                    ToastLength.Short);

                return;
            }
        }

        if (_stream is not { } exhausted) return;

        Toasts.Show(
            this,
            Strings.Format(this, ResourceConstant.String.playback_provider_failed, exhausted.Source.Provider),
            ToastLength.Long);

        // One provider has nothing left to try, but another may still play this. The player stays
        // up on its stopped frame with the controls shown, so Provider is one press away; closing
        // would throw away a source the viewer never got to choose.
        if (exhausted.Providers.Count > 1)
        {
            _playerView?.ShowController();
            return;
        }

        if (!IsFinishing) Close();
    }

    // ExoPlayer's 2000s are input/output and 3000s content parsing, which is what a stream that
    // has gone bad looks like. Decoder, renderer and DRM failures start at 4000 and would fail
    // identically on another of the provider's streams.
    private static bool LooksResolvable(PlaybackException? error)
    {
        return error?.ErrorCode is >= 2000 and < 4000;
    }

    // A livestream is nothing to come back to, and a liveball url is nothing the rail could
    // hold, so neither leaves an entry.
    private void SaveProgress()
    {
        if (_progressSaved || _player is null || _args is null || _playbackStartedAt is null) return;
        if (_stream is not { IsLive: false }) return;
        if (DateTimeOffset.UtcNow - _playbackStartedAt.Value < MinimumSessionForProgress) return;

        var duration = _player.Duration;
        var position = _player.CurrentPosition;

        if (duration <= 0) return;
        if (_args.ToProgress(position, duration) is not { } progress) return;

        _progressSaved = true;

        _ = AppServices.Provider.GetRequiredService<WatchHistoryRecorder>().RecordAsync(progress, default);
    }

    private void ReleasePlayer()
    {
        ControllerMenu.Dismiss();

        if (Detach() is not { } player) return;

        if (_playerView is not null) _playerView.Player = null;

        player.Release();
    }

    // Takes the player off the Activity, leaving the view's reference for its caller to deal
    // with. The position is kept so the next start, a replacement or OnStart, resumes there;
    // a livestream has no there.
    private IExoPlayer? Detach()
    {
        if (_player is not { } player) return null;

        if (_stream is { IsLive: false } && player.CurrentPosition > 0)
            _stream = _stream with { ResumePositionMs = player.CurrentPosition };

        _player = null;

        return player;
    }
}
