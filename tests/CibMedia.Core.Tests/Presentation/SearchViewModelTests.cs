using CibMedia.Core.Catalog;
using CibMedia.Core.Common;
using CibMedia.Core.Presentation.Search;
using CibMedia.Core.Tests.Fakes;
using Xunit;

namespace CibMedia.Core.Tests.Presentation;

public sealed class SearchViewModelTests
{
    [Fact]
    public async Task Holds_a_typed_query_back_until_it_is_long_enough()
    {
        var catalog = new FakeCatalog();
        using var vm = new SearchViewModel(catalog);

        await vm.QueryChangedAsync("a");
        await vm.QueryChangedAsync("ab");

        Assert.Empty(catalog.Searches);
        Assert.Empty(Ready(vm.State));

        await vm.QueryChangedAsync("abc");

        Assert.Equal(["abc"], catalog.Searches);
    }

    [Fact]
    public async Task Searches_a_query_too_short_to_type_when_it_is_submitted()
    {
        var catalog = new FakeCatalog();
        using var vm = new SearchViewModel(catalog);

        await vm.QueryChangedAsync("up");
        await vm.SubmitAsync("up");

        Assert.Equal(["up"], catalog.Searches);
        Assert.NotEmpty(Ready(vm.State));
    }

    [Fact]
    public async Task Does_not_ask_again_for_the_query_it_has_just_answered()
    {
        var catalog = new FakeCatalog();
        using var vm = new SearchViewModel(catalog);

        await vm.QueryChangedAsync("dune");
        await vm.SubmitAsync("dune");

        Assert.Equal(["dune"], catalog.Searches);
    }

    [Fact]
    public async Task Drops_what_it_found_when_the_query_is_backed_out_of()
    {
        var catalog = new FakeCatalog();
        using var vm = new SearchViewModel(catalog);

        await vm.QueryChangedAsync("dune");
        Assert.NotEmpty(Ready(vm.State));

        await vm.QueryChangedAsync("du");

        Assert.Empty(Ready(vm.State));
    }

    private static IReadOnlyList<MediaCard> Ready(SearchState state)
    {
        return Assert.IsType<Load<IReadOnlyList<MediaCard>>.Ready>(state.Results).Value;
    }
}
