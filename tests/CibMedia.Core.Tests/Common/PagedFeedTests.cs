using CibMedia.Core.Catalog;
using CibMedia.Core.Common;
using Xunit;

namespace CibMedia.Core.Tests.Common;

public sealed class PagedFeedTests
{
    [Fact]
    public async Task Accumulates_pages_rather_than_replacing_them()
    {
        var feed = new PagedFeed<MediaCard>((page, _) => Task.FromResult(PageOf(page, 3)));

        await feed.LoadNextAsync(TestContext.Current.CancellationToken);
        var second = await feed.LoadNextAsync(TestContext.Current.CancellationToken);

        Assert.Equal(4, Items(second).Count);
        Assert.Equal("p1-0", Items(second)[0].Title);
        Assert.Equal("p2-1", Items(second)[3].Title);
    }

    [Fact]
    public async Task Stops_asking_once_the_last_page_is_in()
    {
        var calls = 0;
        var feed = new PagedFeed<MediaCard>((page, _) =>
        {
            calls++;
            return Task.FromResult(PageOf(page, 2));
        });

        await feed.LoadNextAsync(TestContext.Current.CancellationToken);
        await feed.LoadNextAsync(TestContext.Current.CancellationToken);
        await feed.LoadNextAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, calls);
        Assert.False(feed.HasMore);
    }

    [Fact]
    public async Task Keeps_what_it_has_when_a_later_page_fails()
    {
        var first = true;
        var feed = new PagedFeed<MediaCard>((page, _) =>
        {
            if (first)
            {
                first = false;
                return Task.FromResult(PageOf(page, 3));
            }

            return Task.FromException<Page<MediaCard>>(new HttpRequestException("dropped"));
        });

        await feed.LoadNextAsync(TestContext.Current.CancellationToken);
        var after = await feed.LoadNextAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, Items(after).Count);
    }

    [Fact]
    public async Task Reports_failure_when_nothing_has_loaded_yet()
    {
        var feed = new PagedFeed<MediaCard>((_, _) =>
            Task.FromException<Page<MediaCard>>(new HttpRequestException("offline")));

        var result = await feed.LoadNextAsync(TestContext.Current.CancellationToken);

        var failed = Assert.IsType<Load<IReadOnlyList<MediaCard>>.Failed>(result);
        Assert.Equal(AppErrorKind.Offline, failed.Error.Kind);
    }

    // Same instance by identity, so a caller can skip a state change and a full page render.
    [Fact]
    public async Task Hands_back_the_same_result_once_there_is_nothing_left_to_load()
    {
        var feed = new PagedFeed<MediaCard>((page, _) => Task.FromResult(PageOf(page, 1)));

        var first = await feed.LoadNextAsync(TestContext.Current.CancellationToken);

        Assert.False(feed.HasMore);

        var again = await feed.LoadNextAsync(TestContext.Current.CancellationToken);

        Assert.Same(first, again);
    }

    [Fact]
    public async Task Fetches_each_page_once_when_paged_concurrently()
    {
        var gate = new Lock();
        var fetched = new List<int>();

        var feed = new PagedFeed<MediaCard>(
            async (page, ct) =>
            {
                lock (gate) fetched.Add(page);

                await Task.Delay(20, ct);

                return PageOf(page, 5);
            });

        await Task.WhenAll(
            Enumerable.Range(0, 8)
                .Select(_ => feed.LoadNextAsync(TestContext.Current.CancellationToken)));

        Assert.Equal(fetched.Count, fetched.Distinct().Count());
    }

    private static IReadOnlyList<MediaCard> Items(Load<IReadOnlyList<MediaCard>> load)
    {
        return Assert.IsType<Load<IReadOnlyList<MediaCard>>.Ready>(load).Value;
    }

    private static Page<MediaCard> PageOf(int page, int totalPages)
    {
        return new Page<MediaCard>(
            [
                .. Enumerable.Range(0, 2).Select(i => new MediaCard(
                    MediaId.Movie((page * 10) + i),
                    $"p{page}-{i}",
                    null,
                    2000,
                    7.0))
            ],
            page,
            totalPages);
    }
}
