using System.Net;
using CibMedia.Playback.Providers;
using Xunit;

namespace CibMedia.Core.Tests.Playback;

public sealed class OutageWindowTests
{
    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.Forbidden)]
    public void Holds_the_long_window_for_an_origin_turning_the_box_away(HttpStatusCode refusal)
    {
        var window = ITmdbPlaybackProvider.OutageFor(new HttpRequestException("refused", null, refusal));

        Assert.Equal(ITmdbPlaybackProvider.RefusedTtl, window);
    }

    // A bad gateway is the upstream having a moment, and the window skips every other title for its
    // length. Five minutes of those over one 502 is what this guards against.
    [Theory]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.NotFound)]
    public void Holds_the_short_window_for_an_upstream_having_a_bad_moment(HttpStatusCode fault)
    {
        var window = ITmdbPlaybackProvider.OutageFor(new HttpRequestException("upstream", null, fault));

        Assert.Equal(ITmdbPlaybackProvider.TimedOutTtl, window);
    }

    [Fact]
    public void Holds_the_short_window_for_a_request_that_ran_out_of_time()
    {
        Assert.Equal(ITmdbPlaybackProvider.TimedOutTtl, ITmdbPlaybackProvider.OutageFor(new TaskCanceledException()));
    }
}
