using Android.Views;
using AndroidX.Leanback.App;
using AndroidX.Leanback.Widget;
using CibMedia.Core.Catalog;
using CibMedia.Core.Common;
using CibMedia.Core.Presentation.Rails;
using CibMedia.AndroidTv.App;
using CibMedia.AndroidTv.Leanback.Presenters;
using CibMedia.AndroidTv.Leanback.Support;
using Microsoft.Extensions.DependencyInjection;

namespace CibMedia.AndroidTv.Leanback.Fragments;

// Every page that is a list of rails. A subclass declares which rails it has, what they are
// called and which view model fills them.
public abstract class RailsFragment<TKey, TViewModel> : RowsSupportFragment
    where TKey : notnull
    where TViewModel : RailsViewModel<TKey>
{
    private const int PrefetchFromEnd = 4;

    // How long the section list has to hold still before this page builds itself. Leanback's
    // own settle logic waits for the headers grid to stop scrolling, and ours never scrolls:
    // four sections fit on screen. Longer than the header's focus animation, short enough to
    // feel like arrival.
    private const int SettleMs = 250;

    private readonly Dictionary<TKey, CardRow<MediaCard>> _rails = [];
    private readonly Dictionary<TKey, Load<IReadOnlyList<MediaCard>>> _rendered = [];
    private readonly List<TKey> _visible = [];

    private bool _abandoned;
    private StateBinding? _binding;
    private Java.Lang.Runnable? _buildRows;
    private RowHeaderHighlight? _headerHighlight;
    private ArrayObjectAdapter? _rows;

    protected TViewModel? ViewModel { get; private set; }

    // True when this page came back to an already-filled view model. The fragment is rebuilt on
    // every section switch, so its own fields cannot tell the two apart.
    protected bool ReusedViewModel { get; private set; }

    private IReadOnlyList<TKey> Order => ViewModel!.Order;

    // Movies and TV Shows are the same class over different media kinds, so the type alone
    // would have them share one view model.
    protected abstract string StateKey { get; }

    protected abstract int TitleOf(TKey key);

    protected abstract TViewModel CreateViewModel(IServiceProvider services);

    public override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        var services = AppServices.Provider;

        ViewModel = services.GetRequiredService<ViewModelStore>()
            .GetOrCreate(StateKey, () => CreateViewModel(services), out var reused);

        ReusedViewModel = reused;

        _binding = StateBinding.Bind(this, ViewModel, Render, false);

        // Started here rather than in BuildRows: fetching costs this thread nothing, so a
        // section arrowed past still gets its data underway.
        _ = ViewModel.EnsureLoadedAsync();
    }

    // Filling the adapter from OnCreate puts roughly 110ms of card binding into the frame that
    // handles the key press, so arrowing down the side panel paid for a whole page per press.
    // Settling on a page still costs the same fill; the sections passed over stop paying for it.
    public override void OnViewCreated(View? view, Bundle? savedInstanceState)
    {
        base.OnViewCreated(view, savedInstanceState);

        // Held in a field and never disposed: the message belongs to the Handler until it runs
        // or is removed, and tearing the managed peer down under it kills the process. The
        // flag makes a fire that outran RemoveCallbacks harmless.
        _buildRows = new Java.Lang.Runnable(() =>
        {
            if (!_abandoned) BuildRows();
        });

        view?.PostDelayed(_buildRows, SettleMs);
    }

    public override void OnDestroyView()
    {
        // The bar belongs to the shell and outlives this page.
        ShowLoading(false);

        _abandoned = true;
        if (_buildRows is not null) View?.RemoveCallbacks(_buildRows);

        base.OnDestroyView();
    }

    public override void OnDestroy()
    {
        if (_headerHighlight is not null)
        {
            ItemViewClicked -= OnItemClicked;
            ItemViewSelected -= OnItemSelected;
            ItemViewSelected -= _headerHighlight.OnSelected;
        }

        _binding?.Dispose();
        _binding = null;
        ViewModel = null;

        base.OnDestroy();
    }

    private void BuildRows()
    {
        if (ViewModel is null || _rows is not null) return;

        var presenters = AppServices.Provider.GetRequiredService<RailPresenters>();

        // A rail's position is its header id, which the paging callback reads back.
        for (var i = 0; i < Order.Count; i++)
            _rails[Order[i]] =
                new CardRow<MediaCard>(i, GetString(TitleOf(Order[i])), presenters.Cards, card => card.Id);

        _rows = new ArrayObjectAdapter(presenters.ListRows);
        Adapter = _rows;

        _headerHighlight = new RowHeaderHighlight(RequireContext());

        ItemViewClicked += OnItemClicked;
        ItemViewSelected += OnItemSelected;
        ItemViewSelected += _headerHighlight.OnSelected;

        // A binding only fires on a change, so the state already held is drawn once here.
        Render();
    }

    private void Render()
    {
        if (ViewModel is null || _rows is null) return;

        var state = ViewModel.State;

        foreach (var key in Order)
        {
            var load = state.For(key);

            // A change notifies the whole page but untouched rails keep the same Load instance,
            // so reference equality is all it takes to skip them. Set wraps every card in a
            // fresh Java peer, so rebuilding ten rails on ten arrivals is thousands of JNI
            // allocations on the UI thread.
            if (_rendered.TryGetValue(key, out var previous) && ReferenceEquals(previous, load))
                continue;

            _rendered[key] = load;
            _rails[key].Set(load);
            PlaceRail(key);
        }

        // Shown while nothing is on screen and something is still outstanding, so a run where
        // every rail fails settles on an empty screen rather than a spinner that never stops.
        ShowLoading(
            _visible.Count == 0
            && Order.Any(key => state.For(key) is Load<IReadOnlyList<MediaCard>>.Loading));
    }

    // The shell's progress bar: a rows fragment's root view is the VerticalGridView itself, so
    // there is nowhere inside it to put one.
    private void ShowLoading(bool loading)
    {
        if ((ParentFragment as BrowseSupportFragment)?.ProgressBarManager is not { } bar) return;

        if (loading)
            bar.Show();
        else
            bar.Hide();
    }

    // A rail that resolved to nothing is removed rather than left empty: an empty row still
    // owns a position, and Down onto it leaves the highlight on a blank line.
    private void PlaceRail(TKey key)
    {
        var shouldShow = _rails[key].HasItems;
        var index = _visible.IndexOf(key);

        if (shouldShow && index < 0)
        {
            var insertAt = Order.TakeWhile(other => !other.Equals(key)).Count(_visible.Contains);
            _visible.Insert(insertAt, key);
            _rows!.Add(insertAt, _rails[key].Row);
            return;
        }

        if (!shouldShow && index >= 0)
        {
            _visible.RemoveAt(index);
            _rows!.RemoveItems(index, 1);
        }
    }

    private void OnItemClicked(object? sender, BaseOnItemViewClickedEventArgs e)
    {
        if (JavaRef.Unwrap<MediaCard>(e.Item) is { } card) Nav.OpenDetails(RequireContext(), card.Id);
    }

    private void OnItemSelected(object? sender, BaseOnItemViewSelectedEventArgs e)
    {
        if (ViewModel is null || e.Row is not ListRow row) return;

        if (Paging.NearEnd(row.Adapter, e.Item, PrefetchFromEnd))
            _ = ViewModel.LoadMoreAsync(Order[(int)row.HeaderItem!.Id]);
    }
}
