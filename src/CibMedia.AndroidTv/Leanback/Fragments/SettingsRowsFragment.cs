using _Microsoft.Android.Resource.Designer;
using Android.Content;
using Android.Text.Format;
using AndroidX.Leanback.App;
using AndroidX.Leanback.Widget;
using CibMedia.Core.Abstractions;
using CibMedia.Core.Abstractions.LocalHttp;
using CibMedia.Core.Common;
using CibMedia.Core.Playback;
using CibMedia.Core.Presentation.Remote;
using CibMedia.AndroidTv.Activities;
using CibMedia.AndroidTv.App;
using CibMedia.AndroidTv.Design;
using CibMedia.AndroidTv.Leanback.Presenters;
using CibMedia.AndroidTv.Leanback.Support;
using CibMedia.AndroidTv.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace CibMedia.AndroidTv.Leanback.Fragments;

// A rows fragment rather than a preferences screen, so it hosts the same cards as everywhere.
public sealed class SettingsRowsFragment : RowsSupportFragment
{
    private const int DeviceIndex = 0;
    private const int DisplayIndex = 1;
    private const int PreferencesIndex = 2;
    private const int KeysIndex = 3;
    private const int CacheIndex = 4;
    private const int HistoryIndex = 5;

    // Static because Resize tears the Activity down, and the flag has to reach the next one.
    private static bool _resumeOnDisplayCard;

    private ArrayObjectAdapter? _actions;
    private ICacheStore? _cache;
    private IPlaybackCache? _playbackCache;
    private PreferencesUiScale? _display;
    private IRemotePlaySwitch? _enabled;
    private IPlaybackHistory? _history;
    private ILocalHttpServer? _server;
    private IApiKeys? _keys;
    private IPlaybackPreferences? _playback;

    public override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        var services = AppServices.Provider;

        _cache = services.GetRequiredService<ICacheStore>();
        _playbackCache = services.GetRequiredService<IPlaybackCache>();
        _display = services.GetRequiredService<PreferencesUiScale>();
        _history = services.GetRequiredService<IPlaybackHistory>();
        _enabled = services.GetRequiredService<IRemotePlaySwitch>();
        _server = services.GetRequiredService<ILocalHttpServer>();
        _keys = services.GetRequiredService<IApiKeys>();
        _playback = services.GetRequiredService<IPlaybackPreferences>();

        _actions = new ArrayObjectAdapter(new SettingsCardPresenter());
        _actions.Add(JavaRef.Wrap(DeviceCard()));
        _actions.Add(JavaRef.Wrap(DisplayCard()));
        _actions.Add(JavaRef.Wrap(PreferencesCard()));
        _actions.Add(JavaRef.Wrap(KeysCard()));
        _actions.Add(JavaRef.Wrap(ClearCacheCard(null)));
        _actions.Add(JavaRef.Wrap(ClearHistoryCard(null)));

        var rows = new ArrayObjectAdapter(RowPresenters.Standard());
        rows.Add(new ListRow(new HeaderItem(0, GetString(ResourceConstant.String.settings_title)), _actions));
        Adapter = rows;

        _enabled.Changed += OnDeviceChanged;
        _server.AddressChanged += OnDeviceChanged;
        ItemViewClicked += OnItemClicked;
    }

    // Posted: the row's cards are bound on the next layout pass.
    public override void OnStart()
    {
        base.OnStart();

        if (!_resumeOnDisplayCard) return;

        _resumeOnDisplayCard = false;

        View?.Post(() =>
        {
            if (!IsAdded) return;

            (ParentFragment as ShellFragment)?.FocusContent();
            SetSelectedPosition(0, false, new ListRowPresenter.SelectItemViewHolderTask(DisplayIndex));
            VerticalGridView?.RequestFocus();
        });
    }

    // Read off disk on every return: the preferences screen, playback and the catalog all
    // write behind this page.
    public override void OnResume()
    {
        base.OnResume();

        _ = RefreshAsync();
    }

    public override void OnDestroy()
    {
        if (_enabled is not null) _enabled.Changed -= OnDeviceChanged;
        if (_server is not null) _server.AddressChanged -= OnDeviceChanged;

        ItemViewClicked -= OnItemClicked;

        base.OnDestroy();
    }

    private void OnItemClicked(object? sender, BaseOnItemViewClickedEventArgs e)
    {
        if (JavaRef.Unwrap<SettingsAction>(e.Item) is not { } action) return;

        switch (action.Id)
        {
            case SettingsActionId.ApiKeys:
                if (Context is { } context) StartActivity(new Intent(context, typeof(SetupActivity)));
                break;
            case SettingsActionId.Device:
                if (_enabled is { } remote) remote.IsOn = !remote.IsOn;
                break;
            case SettingsActionId.DisplaySize:
                Resize();
                break;
            case SettingsActionId.Preferences:
                if (Context is { } host) StartActivity(new Intent(host, typeof(PreferencesActivity)));
                break;
            case SettingsActionId.ClearCache:
                _ = ClearCacheAsync();
                break;
            case SettingsActionId.ClearHistory:
                _ = ClearHistoryAsync();
                break;
        }
    }

    // Switching on binds the port after the switch itself, so the card is drawn again when the
    // address lands.
    private void OnDeviceChanged()
    {
        Activity?.RunOnUiThread(() =>
        {
            if (_actions is { } actions && IsAdded) actions.Replace(DeviceIndex, JavaRef.Wrap(DeviceCard()));
        });
    }

    // Every store, because from the user's seat the cache is one thing. Watch progress is
    // deliberately not in here; it is the other card.
    private async Task ClearCacheAsync()
    {
        if (_cache is null || Context is not { } context) return;

        await ImageCache.ClearAsync(context).ConfigureAwait(true);
        await _cache.ClearAsync(CancellationToken.None).ConfigureAwait(true);
        _playbackCache?.Clear();

        Announce(ResourceConstant.String.settings_cache_cleared);

        await RefreshAsync().ConfigureAwait(true);
    }

    private async Task ClearHistoryAsync()
    {
        if (_history is null) return;

        await _history.ClearAsync(CancellationToken.None).ConfigureAwait(true);

        Announce(ResourceConstant.String.settings_history_cleared);

        await RefreshAsync().ConfigureAwait(true);
    }

    private async Task RefreshAsync()
    {
        if (_cache is null || _history is null || Context is not { } context) return;

        var sizes = await Task.WhenAll(
                _cache.SizeAsync(CancellationToken.None),
                ImageCache.SizeAsync(context))
            .ConfigureAwait(true);

        var recorded = await _history.CountAsync(CancellationToken.None).ConfigureAwait(true);

        // The section can be left while the reads run.
        if (_actions is null || !IsAdded || Context is not { } current) return;

        _actions.Replace(PreferencesIndex, JavaRef.Wrap(PreferencesCard()));
        _actions.Replace(KeysIndex, JavaRef.Wrap(KeysCard()));
        _actions.Replace(CacheIndex, JavaRef.Wrap(ClearCacheCard(CacheSubtitle(current, sizes[0] + sizes[1]))));
        _actions.Replace(HistoryIndex, JavaRef.Wrap(ClearHistoryCard(HistorySubtitle(current, recorded))));
    }

    // Recreated rather than redrawn: density is fixed when the Activity attaches its base
    // context. The FragmentManager puts this section and this card back.
    private void Resize()
    {
        if (_display is not { } display) return;

        display.Step = UiScale.Next(display.Step);
        _resumeOnDisplayCard = true;

        Activity?.Recreate();
    }

    private SettingsAction PreferencesCard()
    {
        var provider = _playback?.DefaultProvider ?? GetString(ResourceConstant.String.provider_default_none);
        var language = _playback?.PreferredLanguage is { } code
            ? PlaybackLanguage.NameOf(code)
            : GetString(ResourceConstant.String.language_default_device);

        return new SettingsAction(
            SettingsActionId.Preferences,
            GetString(ResourceConstant.String.preferences_title),
            $"{provider} · {language}",
            ResourceConstant.Drawable.ic_preferences);
    }

    private SettingsAction DisplayCard()
    {
        var step = _display?.Step ?? UiScaleStep.Normal;

        return new SettingsAction(
            SettingsActionId.DisplaySize,
            GetString(ResourceConstant.String.display_size_title),
            GetString(StepName(step)),
            ResourceConstant.Drawable.ic_display_size);
    }

    private static int StepName(UiScaleStep step)
    {
        return step switch
        {
            UiScaleStep.Smaller => ResourceConstant.String.display_size_smaller,
            UiScaleStep.Small => ResourceConstant.String.display_size_small,
            UiScaleStep.Large => ResourceConstant.String.display_size_large,
            UiScaleStep.Larger => ResourceConstant.String.display_size_larger,
            _ => ResourceConstant.String.display_size_normal
        };
    }

    private SettingsAction KeysCard()
    {
        var subtitle = _keys?.Has(ApiKeyKind.Tmdb) == true
            ? ResourceConstant.String.keys_ready
            : ResourceConstant.String.keys_missing_tmdb;

        return new SettingsAction(
            SettingsActionId.ApiKeys,
            GetString(ResourceConstant.String.keys_title),
            GetString(subtitle),
            ResourceConstant.Drawable.ic_key);
    }

    // The subtitle says what the state is, never what to do about it: switched on, that is the
    // address to post to.
    private SettingsAction DeviceCard()
    {
        var on = _enabled?.IsOn == true;

        var subtitle = (on, _server?.Address) switch
        {
            (false, _) => GetString(ResourceConstant.String.device_off),
            (true, { } address) => address + PlaybackController.Prefix,
            _ => GetString(ResourceConstant.String.device_address_unknown)
        };

        return new SettingsAction(
            SettingsActionId.Device,
            GetString(ResourceConstant.String.device_title),
            subtitle,
            on ? ResourceConstant.Drawable.ic_cast_connected : ResourceConstant.Drawable.ic_cast_offline);
    }

    private SettingsAction ClearCacheCard(string? subtitle)
    {
        return new SettingsAction(
            SettingsActionId.ClearCache,
            GetString(ResourceConstant.String.settings_clear_cache),
            subtitle ?? GetString(ResourceConstant.String.settings_measuring),
            ResourceConstant.Drawable.ic_delete_sweep);
    }

    private SettingsAction ClearHistoryCard(string? subtitle)
    {
        return new SettingsAction(
            SettingsActionId.ClearHistory,
            GetString(ResourceConstant.String.settings_clear_history),
            subtitle ?? GetString(ResourceConstant.String.settings_measuring),
            ResourceConstant.Drawable.ic_history);
    }

    private string CacheSubtitle(Context context, long bytes)
    {
        return bytes <= 0
            ? GetString(ResourceConstant.String.settings_clear_cache_empty)
            : Strings.Format(
                context,
                ResourceConstant.String.settings_clear_cache_size,
                Formatter.FormatShortFileSize(context, bytes) ?? string.Empty);
    }

    private string HistorySubtitle(Context context, int count)
    {
        return count == 0
            ? GetString(ResourceConstant.String.settings_clear_history_empty)
            : Strings.Quantity(context, ResourceConstant.Plurals.settings_clear_history_count, count);
    }

    private void Announce(int messageId)
    {
        if (Context is { } context) Toasts.Show(context, messageId, ToastLength.Short);
    }
}
