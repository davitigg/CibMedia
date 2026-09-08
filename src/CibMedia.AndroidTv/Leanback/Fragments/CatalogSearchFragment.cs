using _Microsoft.Android.Resource.Designer;
using AndroidX.Leanback.App;
using AndroidX.Leanback.Widget;
using CibMedia.Core.Catalog;
using CibMedia.Core.Presentation.Search;
using CibMedia.AndroidTv.App;
using CibMedia.AndroidTv.Leanback.Presenters;
using CibMedia.AndroidTv.Leanback.Support;
using Microsoft.Extensions.DependencyInjection;

namespace CibMedia.AndroidTv.Leanback.Fragments;

// The field, the on-screen keyboard and the voice button are Leanback's.
public sealed class CatalogSearchFragment : SearchSupportFragment, SearchSupportFragment.ISearchResultProvider
{
    private const int PrefetchFromEnd = 8;
    private StateBinding? _binding;
    private CardRow<MediaCard>? _results;
    private ArrayObjectAdapter? _rows;
    private bool _rowShowing;

    private SearchViewModel? _viewModel;

    public ObjectAdapter ResultsAdapter => _rows!;

    // The view model debounces, holds short queries back and cancels what is in flight.
    public bool OnQueryTextChange(string? newQuery)
    {
        _ = _viewModel?.QueryChangedAsync(newQuery ?? string.Empty);
        return true;
    }

    public bool OnQueryTextSubmit(string? query)
    {
        _ = _viewModel?.SubmitAsync(query ?? string.Empty);
        return true;
    }

    public override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        var services = AppServices.Provider;

        _viewModel = ActivatorUtilities.CreateInstance<SearchViewModel>(services);

        // The details pages' presenter but not the rails' view pool: search is its own
        // Activity, and a pooled card holds the Activity that inflated it.
        _results = new CardRow<MediaCard>(
            0,
            GetString(ResourceConstant.String.search_results),
            services.GetRequiredService<DetailPresenters>().Media,
            card => card.Id);

        _rows = new ArrayObjectAdapter(RowPresenters.Standard());

        SetSearchResultProvider(this);

        // Leanback posts a start-recognition runnable of its own shortly after the fragment
        // starts, so arriving at search would press the microphone. Setting a query, even an
        // empty one, is the only way to drop that runnable: the flag is private with no setter.
        SetSearchQuery(string.Empty, false);

        ItemViewClicked += OnItemClicked;
        ItemViewSelected += OnItemSelected;

        _binding = StateBinding.Bind(this, _viewModel, Render);
    }

    public override void OnDestroy()
    {
        ItemViewClicked -= OnItemClicked;
        ItemViewSelected -= OnItemSelected;

        _binding?.Dispose();
        _binding = null;
        _viewModel = null;

        base.OnDestroy();
    }

    // The row is added only once there is something in it, so an empty query shows Leanback's
    // own "no results" state rather than a titled but empty shelf.
    private void Render()
    {
        if (_viewModel is null || _results is null || _rows is null) return;

        _results.Set(_viewModel.State.Results);

        if (_results.HasItems && !_rowShowing)
        {
            _rows.Add(_results.Row);
            _rowShowing = true;
        }
        else if (!_results.HasItems && _rowShowing)
        {
            _rows.RemoveItems(0, 1);
            _rowShowing = false;
        }
    }

    private void OnItemClicked(object? sender, ItemViewClickedEventArgs e)
    {
        if (JavaRef.Unwrap<MediaCard>(e.Item) is { } card) Nav.OpenDetails(RequireContext(), card.Id);
    }

    private void OnItemSelected(object? sender, ItemViewSelectedEventArgs e)
    {
        if (_viewModel is null || e.Row is not ListRow row) return;

        if (Paging.NearEnd(row.Adapter, e.Item, PrefetchFromEnd)) _ = _viewModel.LoadMoreAsync();
    }
}
