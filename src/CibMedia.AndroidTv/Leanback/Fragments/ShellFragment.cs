using _Microsoft.Android.Resource.Designer;
using Android.Views;
using AndroidX.Activity;
using AndroidX.Leanback.App;
using AndroidX.Leanback.Widget;
using CibMedia.Core.Presentation.Browse;
using CibMedia.AndroidTv.App;
using CibMedia.AndroidTv.Design;
using CibMedia.AndroidTv.Leanback.Presenters;
using Java.Lang;
using Microsoft.Extensions.DependencyInjection;
using Action = System.Action;
using Object = Java.Lang.Object;

namespace CibMedia.AndroidTv.Leanback.Fragments;

// Section headers down the left, the selected section's fragment to the right, and the search
// affordance above them. Leanback owns which section is showing and where focus sits.
public sealed class ShellFragment : BrowseSupportFragment
{
    private readonly CancellationTokenSource _warmup = new();
    private OnBackPressedCallback? _back;

    private View? _headersDock;

    // The panel as of the last transition that finished. IsShowingHeaders flips the moment a
    // transition begins, which is too early for anything meant to arrive with the panel.
    private bool _headersSettled;

    private bool _navFocused;
    private View? _titleGroup;

    public override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        HeadersState = HeadersEnabled;

        // Back is driven from OnBackPressedCallback rather than Leanback's own headers back
        // stack, which only pushes an entry on some of the paths into a section.
        HeadersTransitionOnBackEnabled = false;
        BrandColor = Tokens.BgNav(RequireContext());

        // Both colours the same: the orb pulses between them while focused, and that blink was
        // noise. Leanback still zooms it, which is the focus cue.
        SearchAffordanceColors = new SearchOrbView.Colors(
            Tokens.BgPlaceholder(RequireContext()).ToArgb(),
            Tokens.BgPlaceholder(RequireContext()).ToArgb(),
            Tokens.TextPrimary(RequireContext()).ToArgb());

        // A PageRow per section, so each owns its own fragment.
        MainFragmentRegistry!.RegisterFragment(
            Class.FromType(typeof(PageRow)),
            new PageFactory());

        var rows = new ArrayObjectAdapter(new ListRowPresenter());
        rows.Add(Page(PageId.Home, ResourceConstant.String.section_home));
        rows.Add(Page(PageId.Movies, ResourceConstant.String.section_movies));
        rows.Add(Page(PageId.TvShows, ResourceConstant.String.section_tv_shows));
        rows.Add(Page(PageId.Settings, ResourceConstant.String.settings_title));
        Adapter = rows;

        SetOnSearchClickedListener(new SearchClick(() => Nav.OpenSearch(RequireContext())));

        // Not awaited: Home's own requests are the ones that decide when the app looks ready.
        _ = ActivatorUtilities.CreateInstance<CatalogWarmup>(AppServices.Provider)
            .RunAsync(_warmup.Token);
    }

    public override void OnDestroy()
    {
        _warmup.Cancel();
        _warmup.Dispose();

        // The pooled cards belong to this Activity.
        AppServices.Provider.GetRequiredService<RailPresenters>().Clear();

        base.OnDestroy();
    }

    public override void OnViewCreated(View view, Bundle? savedInstanceState)
    {
        base.OnViewCreated(view, savedInstanceState);

        _headersDock = view.FindViewById(ResourceConstant.Id.browse_headers_dock);
        _titleGroup = view.FindViewById(ResourceConstant.Id.browse_title_group);

        // A focus listener only fires for the view it is set on; the tree-wide notification
        // can answer "does the section list hold focus".
        if (view.ViewTreeObserver is { } observer) observer.GlobalFocusChange += OnGlobalFocusChange;

        // Back inside a section opens the section list; Back with the list open leaves the app.
        // Enabled exactly while the list is closed, so the second press falls through.
        _back = new HeadersBack(this) { Enabled = !IsShowingHeaders };
        RequireActivity().OnBackPressedDispatcher.AddCallback(ViewLifecycleOwner, _back);

        // Open at launch with no transition to learn it from.
        _headersSettled = IsShowingHeaders;

        SetBrowseTransitionListener(new HeadersTransition(withHeaders =>
        {
            _back.Enabled = !withHeaders;
            _headersSettled = withHeaders;
            ShowTitle(TitleViewAdapter.FullViewVisible);
        }));
    }

    public override void OnDestroyView()
    {
        if (View?.ViewTreeObserver is { } observer) observer.GlobalFocusChange -= OnGlobalFocusChange;

        _headersDock = null;
        _titleGroup = null;
        _back = null;

        base.OnDestroyView();
    }

    // Leanback restores focus to the section list when the Activity is rebuilt, which is not
    // where the user left it if they were pressing a card.
    public void FocusContent()
    {
        if (IsShowingHeaders) StartHeadersTransition(false);
    }

    // The section list is shorter than the screen, so it is aligned to both edges and does not
    // move; the highlight travels between the items instead. OnStart, not OnViewCreated: the
    // headers are a child fragment whose view does not exist yet when this fragment's does.
    public override void OnStart()
    {
        base.OnStart();

        if (HeadersSupportFragment?.VerticalGridView is { } headers)
        {
            headers.WindowAlignment = BaseGridView.WindowAlignBothEdge;
            headers.WindowAlignmentPreferKeyLineOverLowEdge = false;
            headers.WindowAlignmentPreferKeyLineOverHighEdge = false;

            // Aligning to the top edge puts the first section behind the title bar; the rows'
            // own margin plus padding is where the first row title lands.
            headers.SetPadding(
                headers.PaddingLeft,
                Dimen(ResourceConstant.Dimension.lb_browse_rows_margin_top)
                + Dimen(ResourceConstant.Dimension.lb_browse_padding_top),
                headers.PaddingRight,
                headers.PaddingBottom);
            headers.SetClipToPadding(false);
        }

        InstallBrandMark();
    }

    // At the foot of the section list, so it slides away with the panel. Added to the headers
    // fragment's own root rather than through a layout override, which would mean owning a copy
    // of Leanback's headers layout.
    private void InstallBrandMark()
    {
        if (HeadersSupportFragment?.View is not RelativeLayout root) return;
        if (root.FindViewById(ResourceConstant.Id.headers_brand) is not null) return;

        var logo = new ImageView(RequireContext()) { Id = ResourceConstant.Id.headers_brand };
        logo.SetImageResource(ResourceConstant.Drawable.nav_logo);
        logo.SetScaleType(ImageView.ScaleType.FitCenter);

        var size = Dimen(ResourceConstant.Dimension.brand_mark_size);
        var layout = new RelativeLayout.LayoutParams(size, size);
        layout.AddRule(LayoutRules.AlignParentBottom);
        layout.AddRule(LayoutRules.AlignParentStart);
        layout.SetMargins(
            Dimen(ResourceConstant.Dimension.lb_browse_padding_start),
            0,
            0,
            Dimen(ResourceConstant.Dimension.brand_mark_margin_bottom));

        root.AddView(logo, layout);
    }

    // The title bar holds nothing but the search orb, reachable only up out of the section
    // list, so it is shown exactly while the section list has focus. Both overloads: showTitle(int)
    // picks the components and calls showTitle(boolean), and the browse fragment drives the
    // flags one.
    public override void ShowTitle(bool show)
    {
        base.ShowTitle(TitleVisible);
    }

    public override void ShowTitle(int flags)
    {
        base.ShowTitle(TitleVisible ? TitleViewAdapter.FullViewVisible : 0);
    }

    // Focus moves to the section list a frame before the panel has gone anywhere, so the
    // settled flag holds the orb back until the panel has arrived. A condition rather than a
    // call at the right moment because Leanback calls showTitle itself during the transition.
    private bool TitleVisible => _navFocused && _headersSettled;

    // The orb counts as part of the nav: hiding the bar the moment focus lands on it would
    // take the focus straight back off it.
    private void OnGlobalFocusChange(object? sender, ViewTreeObserver.GlobalFocusChangeEventArgs e)
    {
        var inNav = Contains(_headersDock, e.NewFocus) || Contains(_titleGroup, e.NewFocus);

        if (inNav == _navFocused) return;

        _navFocused = inNav;
        ShowTitle(TitleViewAdapter.FullViewVisible);
    }

    private static bool Contains(View? ancestor, View? view)
    {
        if (ancestor is null) return false;

        for (var node = view; node is not null; node = node.Parent as View)
            if (node == ancestor)
                return true;

        return false;
    }

    private int Dimen(int id)
    {
        return Resources!.GetDimensionPixelSize(id);
    }

    private PageRow Page(long id, int titleId)
    {
        return new PageRow(new HeaderItem(id, GetString(titleId)));
    }

    private sealed class PageFactory : FragmentFactory
    {
        public override Object CreateFragment(Object? rowObj)
        {
            return (rowObj as Row)?.HeaderItem?.Id switch
            {
                PageId.Movies => BrowseRowsFragment.ForMovies(),
                PageId.TvShows => BrowseRowsFragment.ForTvShows(),
                PageId.Settings => new SettingsRowsFragment(),
                _ => new HomeRowsFragment()
            };
        }
    }

    private sealed class HeadersBack(ShellFragment shell) : OnBackPressedCallback(true)
    {
        public override void HandleOnBackPressed()
        {
            shell.StartHeadersTransition(true);
        }
    }

    private sealed class HeadersTransition(Action<bool> onStop) : BrowseTransitionListener
    {
        public override void OnHeadersTransitionStop(bool withHeaders)
        {
            onStop(withHeaders);
        }
    }

    private sealed class SearchClick(Action onClick)
        : Object, View.IOnClickListener
    {
        public void OnClick(View? v)
        {
            onClick();
        }
    }
}
