using CibMedia.Core.Abstractions;
using CibMedia.Core.Catalog;
using CibMedia.Core.Common;

namespace CibMedia.Core.Presentation.Search;

public sealed class SearchViewModel : ViewModel<SearchState>
{
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(350);

    // Typing on a D-pad outlasts any debounce, so holding the first two letters back is what
    // actually cuts requests. Submitting is exempt: Up, It and 24 are all titles.
    private const int MinimumTypedLength = 3;

    private readonly ICatalog _catalog;
    private readonly CancellationScope _queries;

    private PagedFeed<MediaCard>? _feed;
    private string? _searched;

    public SearchViewModel(ICatalog catalog)
        : base(SearchState.Initial)
    {
        _catalog = catalog;
        _queries = new CancellationScope(Lifetime);
    }

    // A keystroke: debounced, and the request in flight cancelled before a new one starts.
    public Task QueryChangedAsync(string query)
    {
        return SearchAsync(query, true);
    }

    // The search action: whatever is in the field, however short, and now.
    public Task SubmitAsync(string query)
    {
        return SearchAsync(query, false);
    }

    private async Task SearchAsync(string query, bool typed)
    {
        var token = await _queries.NextAsync().ConfigureAwait(false);

        SetState(s => s with { Query = query });

        if (string.IsNullOrWhiteSpace(query) || (typed && query.Length < MinimumTypedLength))
        {
            _feed = null;
            _searched = null;
            SetState(s => s with { Results = Load.Ready<IReadOnlyList<MediaCard>>([]) });
            return;
        }

        // A query already answered is not asked again; one that failed is, which makes the
        // search action the retry.
        if (_searched == query && State.Results is not Load<IReadOnlyList<MediaCard>>.Failed) return;

        if (typed)
        {
            try
            {
                await Task.Delay(Debounce, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        SetState(s => s with { Results = Load.Loading<IReadOnlyList<MediaCard>>() });

        _searched = query;
        _feed = new PagedFeed<MediaCard>((page, ct) => _catalog.SearchAsync(query, page, ct), PagedFeed.Unlimited);

        var results = await _feed.LoadNextAsync(token).ConfigureAwait(false);

        if (!token.IsCancellationRequested) SetState(s => s with { Results = results });
    }

    public async Task LoadMoreAsync()
    {
        if (_feed is null || !_feed.HasMore) return;

        var results = await _feed.LoadNextAsync(Lifetime).ConfigureAwait(false);
        SetState(s => s with { Results = results });
    }

    protected override void OnDispose()
    {
        _queries.Dispose();
    }
}
