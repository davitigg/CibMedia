using _Microsoft.Android.Resource.Designer;
using Android.Graphics.Drawables;
using Android.Views;
using AndroidX.Core.Content;
using AndroidX.Leanback.App;
using AndroidX.Leanback.Widget;
using CibMedia.Core.Abstractions;
using CibMedia.Core.Catalog;
using CibMedia.Core.Common;
using CibMedia.Core.Playback;
using CibMedia.AndroidTv.App;
using CibMedia.AndroidTv.Design;
using CibMedia.AndroidTv.Leanback.Presenters;
using CibMedia.AndroidTv.Leanback.Support;
using CibMedia.AndroidTv.Platform;
using Java.Lang;
using Microsoft.Extensions.DependencyInjection;
using LbAction = AndroidX.Leanback.Widget.Action;
using Object = Java.Lang.Object;

namespace CibMedia.AndroidTv.Leanback.Fragments;

// Everything a details page does regardless of what it shows: the hero with its parallax
// backdrop, the title block, the Play button and the rails underneath.
public abstract class DetailsFragmentBase : DetailsSupportFragment
{
    protected const long ActionPlay = 1;
    protected const long ActionPlayFromStart = 2;
    protected const long ActionProvider = 3;

    private readonly List<ListRow> _rails = [];

    private DetailsSupportFragmentBackgroundController? _background;
    private string? _boundBackdropUrl;
    private Drawable? _restartIcon;
    private Drawable? _providerIcon;
    private ArrayObjectAdapter? _actions;
    private LbAction? _providerAction;
    private string? _boundProvider;
    private bool _boundPlayFromStart;
    private string? _boundPlayLabel;
    private string? _boundPosterUrl;
    private RowHeaderHighlight _headerHighlight = null!;
    private bool _loading;
    private Drawable? _playIcon;

    protected MediaId MediaId { get; private set; }

    protected IImageLoader Images { get; private set; } = null!;

    protected ArrayObjectAdapter RowsAdapter { get; private set; } = null!;

    protected DetailsOverviewRow Overview { get; private set; } = null!;

    protected abstract void StartLoading();

    public override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        var services = AppServices.Provider;
        Images = services.GetRequiredService<IImageLoader>();
        MediaId = IntentExtras.GetMediaId(Activity?.Intent) ?? new MediaId(0, MediaKind.Movie);

        var context = RequireContext();

        // The classic overview presenter lays the poster out beside the text with the actions
        // underneath, as one block. It is [Obsolete] in favour of the full-width one, which
        // grows the description on the first Down and puts its actions in a bar across the
        // screen; the whole of Leanback is deprecated for Compose for TV, which .NET cannot use.
#pragma warning disable CS0618
        var overviewPresenter = new DetailsOverviewRowPresenter(new SynopsisPresenter())
        {
            OnActionClickedListener = new ActionClick(OnActionClicked),
            BackgroundColor = Tokens.BgSurface(context).ToArgb()
        };
#pragma warning restore CS0618

        var selector = new ClassPresenterSelector();
        selector.AddClassPresenter(Class.FromType(typeof(DetailsOverviewRow)), overviewPresenter);
        selector.AddClassPresenter(Class.FromType(typeof(ListRow)), RowPresenters.Standard());

        Overview = new DetailsOverviewRow(JavaRef.Wrap(new Synopsis(string.Empty, null, null)));
        Overview.ActionsAdapter = new ArrayObjectAdapter();

        RowsAdapter = new ArrayObjectAdapter(selector);
        RowsAdapter.Add(Overview);
        Adapter = RowsAdapter;

        _background = new DetailsSupportFragmentBackgroundController(this);
        _background.EnableParallax();
        _background.SolidColor = Tokens.BgBase(context);

        _headerHighlight = new RowHeaderHighlight(context);
        ItemViewSelected += _headerHighlight.OnSelected;
    }

    public override void OnDestroy()
    {
        ItemViewSelected -= _headerHighlight.OnSelected;

        base.OnDestroy();
    }

    // Not in OnCreate: a subclass builds its rails in its own OnCreate after calling base, and a
    // cached response resolves on the UI thread, where the render runs inline.
    public override void OnViewCreated(View view, Bundle? savedInstanceState)
    {
        base.OnViewCreated(view, savedInstanceState);

        if (_loading) return;

        _loading = true;
        StartLoading();
    }

    // Called on every state change. Only the text is cheap enough to restate each time.
    protected void BindHeader(
        Synopsis synopsis,
        string? posterUrl,
        string? backdropUrl,
        string? playLabel = null,
        bool canPlayFromStart = false,
        Load<IReadOnlyList<string>>? providers = null,
        string? provider = null)
    {
        Overview.Item = JavaRef.Wrap(synopsis);

        var label = playLabel ?? GetString(ResourceConstant.String.action_play);

        // The provider action is there from the first draw, as a dash until the title resolves, so
        // the answer only renames it: rebuilding the row moves focus back to Play, and neither the
        // answer landing nor a provider step may move focus off the action that has it.
        var providerLabel = ProviderLabel(providers, provider);
        var sameActions = label == _boundPlayLabel && canPlayFromStart == _boundPlayFromStart;

        if (sameActions && providerLabel != _boundProvider && _providerAction is { } current)
        {
            _boundProvider = providerLabel;
            current.Label1Formatted = new Java.Lang.String(providerLabel);
            _actions?.NotifyArrayItemRangeChanged(_actions.IndexOf(current), 1);
        }
        else if (!sameActions)
        {
            _boundPlayLabel = label;
            _boundPlayFromStart = canPlayFromStart;
            _boundProvider = providerLabel;

            _playIcon ??= ContextCompat.GetDrawable(RequireContext(), ResourceConstant.Drawable.ic_play);

            var action = new LbAction(ActionPlay) { Icon = _playIcon };
            action.Label1Formatted = new Java.Lang.String(label);

            var actions = new ArrayObjectAdapter();
            actions.Add(action);

            if (canPlayFromStart)
            {
                _restartIcon ??=
                    ContextCompat.GetDrawable(RequireContext(), ResourceConstant.Drawable.ic_replay);

                var restart = new LbAction(ActionPlayFromStart) { Icon = _restartIcon };
                restart.Label1Formatted =
                    new Java.Lang.String(GetString(ResourceConstant.String.action_play_from_start));
                actions.Add(restart);
            }

            // Named for the provider it will play from; a press moves to the next, and with one
            // provider it stays as it is and only says which.
            _providerIcon ??=
                ContextCompat.GetDrawable(RequireContext(), ResourceConstant.Drawable.ic_provider_action);

            var next = new LbAction(ActionProvider) { Icon = _providerIcon };
            next.Label1Formatted = new Java.Lang.String(providerLabel);
            actions.Add(next);
            _providerAction = next;

            _actions = actions;
            Overview.ActionsAdapter = actions;
        }

        var context = RequireContext();

        if (posterUrl != _boundPosterUrl)
        {
            _boundPosterUrl = posterUrl;

            Artwork.LoadDrawable(
                context,
                posterUrl,
                Tokens.CardWidth(context),
                Tokens.CardFocusedHeight(context),
                drawable => Overview.ImageDrawable = drawable);
        }

        // The largest bitmap in the app, so it is not re-decoded for a season change.
        if (backdropUrl != _boundBackdropUrl)
        {
            _boundBackdropUrl = backdropUrl;

            Artwork.LoadBitmap(
                context,
                backdropUrl,
                Tokens.HeroBackdropWidth(context),
                Tokens.HeroHeight(context),
                bitmap => _background!.CoverBitmap = bitmap);
        }
    }

    // The header is the one thing always on screen, so a failed load is reported there.
    protected void BindError(AppError error)
    {
        BindHeader(new Synopsis(ErrorMessages.For(RequireContext(), error), null, null), null, null);
    }

    protected static string? GenreLine(IReadOnlyList<GenreRef> genres)
    {
        return genres.Count > 0 ? string.Join(", ", genres.Select(genre => genre.Name)) : null;
    }

    // Keeps the adapter to the rails that have cards, so Down cannot land on an empty row. Only
    // the rows that changed are touched: rebuilding wholesale would detach the focused card,
    // and on a series page the state changes on every step along the seasons rail.
    protected void SyncRows(params (bool Show, ListRow Row)[] rails)
    {
        var wanted = rails.Where(rail => rail.Show).Select(rail => rail.Row).ToList();

        if (wanted.Count == _rails.Count
            && !wanted.Where((row, i) => !ReferenceEquals(row, _rails[i])).Any())
            return;

        // Position 0 is the overview row.
        for (var i = 0; i < wanted.Count; i++)
        {
            if (i < _rails.Count)
            {
                if (ReferenceEquals(_rails[i], wanted[i])) continue;

                // Replaced in place: removing and re-adding rebuilds the row's views, which
                // reads as the rail blinking. A retitled rail shares its item adapter.
                RowsAdapter.Replace(i + 1, wanted[i]);
                continue;
            }

            RowsAdapter.Add(i + 1, wanted[i]);
        }

        for (var i = _rails.Count - 1; i >= wanted.Count; i--) RowsAdapter.RemoveItems(i + 1, 1);

        _rails.Clear();
        _rails.AddRange(wanted);
    }

    // The position comes from the list SyncRows settled on, since a hidden rail is not in the
    // adapter at all. Posted because the row's view holder is created during the layout that
    // has not happened yet.
    protected void FocusCard(ListRow row, int itemIndex)
    {
        var position = _rails.IndexOf(row);

        if (position < 0) return;

        View?.Post(() => RowsSupportFragment?.SetSelectedPosition(
            position + 1,
            true,
            new ListRowPresenter.SelectItemViewHolderTask(itemIndex)));
    }

    // The dash is "not known": not asked yet, or asked offline. A title the API has nothing for
    // says so; Play stays as it is and explains when pressed, the way an episode card does.
    private string ProviderLabel(Load<IReadOnlyList<string>>? providers, string? provider)
    {
        return providers switch
        {
            Load<IReadOnlyList<string>>.Ready when provider is not null => provider,
            Load<IReadOnlyList<string>>.Failed { Error.Kind: AppErrorKind.NotFound } =>
                GetString(ResourceConstant.String.action_provider_none),
            _ => GetString(ResourceConstant.String.action_provider_pending)
        };
    }

    private void OnActionClicked(LbAction action)
    {
        if (action.Id == ActionPlay) OnPlay(false);
        else if (action.Id == ActionPlayFromStart) OnPlay(true);
        else if (action.Id == ActionProvider) OnNextProvider();
    }

    protected string? PlayLabel(bool resuming, string? episodeLabel, WatchProgress? progress)
    {
        var context = RequireContext();

        var basis = (resuming, episodeLabel) switch
        {
            (true, { } episode) => Strings.Format(
                context, ResourceConstant.String.action_resume_episode, episode),
            (true, null) => GetString(ResourceConstant.String.action_resume),
            (false, { } episode) => Strings.Format(
                context, ResourceConstant.String.action_play_episode, episode),
            _ => null
        };

        if (!resuming || basis is null || progress is not { RemainingMs: > 0 } left) return basis;

        return Strings.Format(
            context,
            ResourceConstant.String.action_resume_left,
            basis,
            Remaining(context, left.RemainingMs));
    }

    // Rounded up, so the last stretch reads as a minute left rather than none.
    private static string Remaining(Android.Content.Context context, long remainingMs)
    {
        var minutes = System.Math.Max(1, (int)System.Math.Round(remainingMs / 60_000d));

        return minutes < 60
            ? Strings.Format(context, ResourceConstant.String.action_minutes, minutes)
            : Strings.Format(
                context, ResourceConstant.String.action_hours_minutes, minutes / 60, minutes % 60);
    }

    protected abstract void OnPlay(bool fromStart);

    protected abstract void OnNextProvider();

    private sealed class ActionClick(Action<LbAction> onClick)
        : Object, IOnActionClickedListener
    {
        public void OnActionClicked(LbAction? action)
        {
            if (action is not null) onClick(action);
        }
    }
}
