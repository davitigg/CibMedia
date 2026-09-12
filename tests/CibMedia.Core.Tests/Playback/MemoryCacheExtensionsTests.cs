using CibMedia.Playback.Extensions;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace CibMedia.Core.Tests.Playback;

public sealed class MemoryCacheExtensionsTests
{
    private static readonly TimeSpan AMinute = TimeSpan.FromMinutes(1);

    // Details and playback resolve the same title separately and overlap, which used to run every
    // upstream request twice.
    [Fact]
    public async Task Runs_one_lookup_for_callers_that_overlap()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var gate = new TaskCompletionSource();
        var runs = 0;

        ValueTask<int> Factory(CancellationToken token)
        {
            Interlocked.Increment(ref runs);

            return new ValueTask<int>(gate.Task.ContinueWith(_ => 7, TaskScheduler.Default));
        }

        var first = cache.GetOrStoreAsync("k", Factory, AMinute, TestContext.Current.CancellationToken);
        var second = cache.GetOrStoreAsync("k", Factory, AMinute, TestContext.Current.CancellationToken);

        gate.SetResult();

        Assert.Equal(7, await first);
        Assert.Equal(7, await second);
        Assert.Equal(1, runs);
    }

    [Fact]
    public async Task Answers_the_second_caller_from_the_cache()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var runs = 0;

        ValueTask<int> Factory(CancellationToken token)
        {
            Interlocked.Increment(ref runs);

            return new ValueTask<int>(7);
        }

        await cache.GetOrStoreAsync("k", Factory, AMinute, TestContext.Current.CancellationToken);
        await cache.GetOrStoreAsync("k", Factory, AMinute, TestContext.Current.CancellationToken);

        Assert.Equal(1, runs);
    }

    // The shared run belongs to no one caller, so the one that walks away leaves it standing.
    [Fact]
    public async Task Leaves_the_work_running_when_a_caller_gives_up()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var leaving = new CancellationTokenSource();
        var gate = new TaskCompletionSource();

        ValueTask<int> Factory(CancellationToken token) =>
            new(gate.Task.ContinueWith(_ => 7, TaskScheduler.Default));

        var abandoned = cache.GetOrStoreAsync("k", Factory, AMinute, leaving.Token);
        var waiting = cache.GetOrStoreAsync("k", Factory, AMinute, TestContext.Current.CancellationToken);

        await leaving.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await abandoned);

        gate.SetResult();

        Assert.Equal(7, await waiting);
    }

    // A throw caches nothing and leaves no flight behind, or one upstream blip would be answered
    // with the same exception for as long as anyone kept asking.
    [Fact]
    public async Task Retries_after_a_lookup_that_threw()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var runs = 0;

        ValueTask<int> Factory(CancellationToken token)
        {
            return ++runs is 1
                ? ValueTask.FromException<int>(new HttpRequestException("upstream"))
                : new ValueTask<int>(7);
        }

        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await cache.GetOrStoreAsync("k", Factory, AMinute, TestContext.Current.CancellationToken));

        Assert.Equal(7, await cache.GetOrStoreAsync("k", Factory, AMinute, TestContext.Current.CancellationToken));
        Assert.Equal(2, runs);
    }
}
