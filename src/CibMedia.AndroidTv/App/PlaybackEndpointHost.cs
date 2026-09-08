using _Microsoft.Android.Resource.Designer;
using Android.App;
using Android.Widget;
using CibMedia.Core.Abstractions;
using CibMedia.Core.Common;
using CibMedia.Core.Abstractions.LocalHttp;
using CibMedia.Core.Playback;
using CibMedia.Core.Presentation.Remote;
using CibMedia.AndroidTv.Design;
using CibMedia.AndroidTv.Playback;
using Object = Java.Lang.Object;

namespace CibMedia.AndroidTv.App;

// Serves the playback endpoints only while remote play is switched on and an Activity of this
// app is started: Android will not let a backgrounded app start an Activity, so a command
// arriving then would have nowhere to go.
public sealed class PlaybackEndpointHost(
    ILocalHttpServer server,
    IRemotePlaySwitch enabled,
    PlaybackController playback,
    ICatalog catalog)
    : Object, Application.IActivityLifecycleCallbacks
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    private Activity? _resumed;
    private IAsyncDisposable? _serving;
    private int _started;

    public void Attach(Application application)
    {
        playback.OpenRequested += OnOpenRequested;
        playback.PlayRequested += OnPlayRequested;
        enabled.Changed += OnSwitchChanged;
        application.RegisterActivityLifecycleCallbacks(this);
    }

    public void OnActivityStarted(Activity activity)
    {
        if (++_started == 1 && enabled.IsOn) _ = ServeAsync();
    }

    // Moving between this app's own Activities overlaps, so this only reaches zero when the app
    // itself leaves the screen.
    public void OnActivityStopped(Activity activity)
    {
        if (--_started == 0) _ = ReleaseAsync();
    }

    public void OnActivityResumed(Activity activity)
    {
        _resumed = activity;
    }

    public void OnActivityPaused(Activity activity)
    {
        if (ReferenceEquals(_resumed, activity)) _resumed = null;
    }

    public void OnActivityCreated(Activity activity, Bundle? savedInstanceState)
    {
    }

    public void OnActivityDestroyed(Activity activity)
    {
    }

    public void OnActivitySaveInstanceState(Activity activity, Bundle outState)
    {
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _gate.Dispose();

        base.Dispose(disposing);
    }

    private void OnSwitchChanged()
    {
        if (enabled.IsOn && _started > 0)
        {
            _ = ServeAsync();
            return;
        }

        _ = ReleaseAsync();
    }

    private async Task ServeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            _serving ??= await server.ServeAsync(playback, CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task ReleaseAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_serving is not { } serving) return;

            _serving = null;

            await serving.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private void OnOpenRequested(TitleTarget target)
    {
        _ = OpenAsync(target);
    }

    // The page opens only once the catalogue owns up to the title. A number that names nothing
    // would otherwise open a details page with an error where its title should be, and a Play
    // button that leads nowhere.
    private async Task OpenAsync(TitleTarget target)
    {
        var missing = await TitleCheck.MissingAsync(catalog, target, CancellationToken.None)
            .ConfigureAwait(false);

        if (_resumed is not { } activity) return;

        activity.RunOnUiThread(() =>
        {
            if (missing is { } error)
            {
                Toasts.Show(
                    activity,
                    error.Kind is AppErrorKind.NotFound
                        ? Strings.Format(activity, ResourceConstant.String.remote_unknown_id, target.Id.TmdbId)
                        : activity.GetString(ResourceConstant.String.remote_catalogue_unreachable),
                    ToastLength.Long);

                return;
            }

            Nav.OpenDetails(activity, target.Id, target.SeasonNumber, target.EpisodeNumber);
        });
    }

    // A liveball url arrives with nothing describing it, so the player opens on the url alone
    // and its heading stays empty.
    private void OnPlayRequested(LiveballTarget target)
    {
        if (_resumed is not { } activity) return;

        activity.RunOnUiThread(() =>
            Nav.OpenPlayback(activity, new PlaybackArgs(target, string.Empty)));
    }
}
