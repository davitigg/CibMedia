using CibMedia.Core.Catalog;
using CibMedia.Core.Common;
using CibMedia.Core.Presentation.Rails;
using Xunit;

namespace CibMedia.Core.Tests.Presentation;

public sealed class RailsViewModelTests
{
    [Fact]
    public async Task Asks_again_when_the_page_it_settled_on_had_a_rail_missing()
    {
        var source = new Source { Fails = true };
        var page = new RailsPage(source);

        await page.EnsureLoadedAsync();

        Assert.Equal(1, source.Attempts);
        Assert.IsType<Load<IReadOnlyList<MediaCard>>.Failed>(page.State.For("rail"));

        source.Fails = false;
        await page.EnsureLoadedAsync();

        Assert.Equal(2, source.Attempts);
        Assert.True(page.State.For("rail").HasValue);
    }

    [Fact]
    public async Task Does_not_ask_again_once_the_page_came_back_whole()
    {
        var source = new Source { Fails = false };
        var page = new RailsPage(source);

        await page.EnsureLoadedAsync();
        await page.EnsureLoadedAsync();
        await page.EnsureLoadedAsync();

        Assert.Equal(1, source.Attempts);
    }

    [Fact]
    public async Task Asks_only_for_the_rail_that_was_missing()
    {
        var good = new Source { Fails = false, TotalPages = 5 };
        var bad = new Source { Fails = true };
        var page = new RailsPage(good, bad);

        await page.EnsureLoadedAsync();
        Assert.Equal(1, good.Attempts);
        Assert.Equal(1, bad.Attempts);

        bad.Fails = false;
        await page.EnsureLoadedAsync();

        Assert.Equal(1, good.Attempts);
        Assert.Equal(2, bad.Attempts);
    }

    [Fact]
    public async Task Joins_a_load_already_running()
    {
        var gate = new TaskCompletionSource();
        var source = new Source { Fails = false, Gate = gate };
        var page = new RailsPage(source);

        var first = page.EnsureLoadedAsync();
        var second = page.EnsureLoadedAsync();

        gate.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(1, source.Attempts);
    }

    private sealed class Source
    {
        private int _attempts;

        public bool Fails { get; set; }

        public TaskCompletionSource? Gate { get; init; }

        public int TotalPages { get; init; } = 1;

        public int Attempts => _attempts;

        public async Task<Page<MediaCard>> FetchAsync(int page, CancellationToken ct)
        {
            Interlocked.Increment(ref _attempts);

            if (Gate is not null) await Gate.Task.ConfigureAwait(false);
            if (Fails) throw new HttpRequestException("TMDb is down");

            return new Page<MediaCard>(
                [new MediaCard(MediaId.Movie(1), "Film", null, null, null, null)], page, TotalPages);
        }
    }

    private sealed class RailsPage : RailsViewModel<string>
    {
        public RailsPage(Source source)
            : base([new Rail<string>("rail", source.FetchAsync)])
        {
        }

        public RailsPage(Source good, Source bad)
            : base([new Rail<string>("good", good.FetchAsync), new Rail<string>("rail", bad.FetchAsync)])
        {
        }
    }
}
