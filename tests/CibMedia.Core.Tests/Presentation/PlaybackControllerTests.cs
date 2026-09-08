using CibMedia.Core.Abstractions.LocalHttp;
using CibMedia.Core.Catalog;
using CibMedia.Core.Playback;
using CibMedia.Core.Presentation.Remote;
using Xunit;

namespace CibMedia.Core.Tests.Presentation;

public sealed class PlaybackControllerTests
{
    [Fact]
    public async Task Opens_a_movie_by_its_tmdb_id()
    {
        var (controller, opened, _) = Build();

        var response = await Post(controller, "/playback/movie", """{"tmdbId":603}""");

        Assert.Equal(202, response.Status);
        Assert.Equal([new TitleTarget(MediaId.Movie(603))], opened);
    }

    [Fact]
    public async Task Opens_a_show_at_the_episode_it_was_given()
    {
        var (controller, opened, _) = Build();

        var response = await Post(controller, "/playback/tv-show", """{"tmdbId":1396,"season":2,"episode":5}""");

        Assert.Equal(202, response.Status);
        Assert.Equal([new TitleTarget(MediaId.TvShow(1396), 2, 5)], opened);
    }

    [Fact]
    public async Task Opens_a_show_with_no_episode_named()
    {
        var (controller, opened, _) = Build();

        await Post(controller, "/playback/tv-show", """{"tmdbId":1396}""");

        Assert.Equal([new TitleTarget(MediaId.TvShow(1396))], opened);
    }

    [Fact]
    public async Task Plays_a_liveball_url_rather_than_opening_a_page()
    {
        var (controller, opened, played) = Build();

        var response = await Post(controller, "/playback/liveball", """{"url":"https://liveball.test/match/7"}""");

        Assert.Equal(202, response.Status);
        Assert.Equal([new LiveballTarget("https://liveball.test/match/7")], played);
        Assert.Empty(opened);
    }

    // Which hosts the API is willing to fetch is the API's to say, so a well-formed url on any
    // host is passed along and answered for there.
    [Fact]
    public async Task Passes_along_a_well_formed_url_the_api_may_still_turn_down()
    {
        var (controller, _, played) = Build();

        var response = await Post(controller, "/playback/liveball", """{"url":"https://example.com/nope"}""");

        Assert.Equal(202, response.Status);
        Assert.Equal([new LiveballTarget("https://example.com/nope")], played);
    }

    [Fact]
    public async Task Plays_a_liveball_url_it_was_given_padded()
    {
        var (controller, _, played) = Build();

        await Post(controller, "/playback/liveball", """{"url":"  https://liveball.test/match/7  "}""");

        Assert.Equal([new LiveballTarget("https://liveball.test/match/7")], played);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"url":null}""")]
    [InlineData("""{"url":""}""")]
    [InlineData("""{"url":"   "}""")]
    // The sender is the one that can fix these, and it is still holding the connection.
    [InlineData("""{"url":"not-a-url"}""")]
    [InlineData("""{"url":"/match/1553163"}""")]
    [InlineData("""{"url":"liveball.sx/match/1553163"}""")]
    [InlineData("""{"url":"ftp://liveball.sx/match/1553163"}""")]
    [InlineData("""{"url":"javascript:alert(1)"}""")]
    public async Task Turns_down_a_liveball_command_that_names_no_url(string body)
    {
        var (controller, _, played) = Build();

        var response = await Post(controller, "/playback/liveball", body);

        Assert.Equal(400, response.Status);
        Assert.Empty(played);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("null")]
    public async Task Turns_down_a_body_it_cannot_read_without_opening_anything(string body)
    {
        var (controller, opened, _) = Build();

        var response = await Post(controller, "/playback/movie", body);

        Assert.Equal(400, response.Status);
        Assert.Empty(opened);
    }

    [Fact]
    public void Serves_a_movie_a_show_and_a_liveball_route_and_nothing_else()
    {
        var (controller, _, _) = Build();

        Assert.Equal(
            [("POST", "/playback/movie"), ("POST", "/playback/tv-show"), ("POST", "/playback/liveball")],
            controller.Routes.Select(r => (r.Method, r.Path)));
    }

    private static (PlaybackController, List<TitleTarget>, List<LiveballTarget>) Build()
    {
        var controller = new PlaybackController();
        var opened = new List<TitleTarget>();
        var played = new List<LiveballTarget>();

        controller.OpenRequested += opened.Add;
        controller.PlayRequested += played.Add;

        return (controller, opened, played);
    }

    private static Task<LocalHttpResponse> Post(PlaybackController controller, string path, string body)
    {
        var route = controller.Routes.Single(r => r.Method == "POST" && r.Path == path);

        return route.Handler(new LocalHttpRequest("POST", path, body), TestContext.Current.CancellationToken);
    }
}
