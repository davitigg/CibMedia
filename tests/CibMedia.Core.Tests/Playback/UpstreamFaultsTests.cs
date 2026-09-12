using CibMedia.Playback.Extensions;
using CibMedia.Playback.Providers;
using Xunit;

namespace CibMedia.Core.Tests.Playback;

public sealed class UpstreamFaultsTests
{
    // The outage window skips every other title for its length, so it may only open on evidence
    // about the upstream. A budget this side set is evidence about one lookup and nothing more.
    [Fact]
    public void Does_not_read_a_budget_of_ours_as_an_upstream_fault()
    {
        var expired = new PlaybackTimeoutException("the xpass player for tv/1/1/1", new OperationCanceledException());

        Assert.False(expired.IsUpstreamFault(CancellationToken.None));
    }

    [Fact]
    public void Reads_a_refusal_and_an_unasked_cancellation_as_upstream_faults()
    {
        Assert.True(new HttpRequestException("refused").IsUpstreamFault(CancellationToken.None));
        Assert.True(new OperationCanceledException().IsUpstreamFault(CancellationToken.None));
    }

    [Fact]
    public void Reads_the_caller_giving_up_as_no_fault_at_all()
    {
        Assert.False(new OperationCanceledException().IsUpstreamFault(new CancellationToken(canceled: true)));
    }
}
