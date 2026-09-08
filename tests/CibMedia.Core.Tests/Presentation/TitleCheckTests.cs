using CibMedia.Core.Catalog;
using CibMedia.Core.Common;
using CibMedia.Core.Playback;
using CibMedia.Core.Presentation.Remote;
using CibMedia.Core.Tests.Fakes;
using Xunit;

namespace CibMedia.Core.Tests.Presentation;

public sealed class TitleCheckTests
{
    [Fact]
    public async Task Owns_up_to_a_movie_the_catalogue_has()
    {
        var missing = await TitleCheck.MissingAsync(
            new FakeCatalog(), new TitleTarget(MediaId.Movie(603)), TestContext.Current.CancellationToken);

        Assert.Null(missing);
    }

    [Fact]
    public async Task Owns_up_to_a_show_the_catalogue_has()
    {
        var missing = await TitleCheck.MissingAsync(
            new FakeCatalog(), new TitleTarget(MediaId.TvShow(1396)), TestContext.Current.CancellationToken);

        Assert.Null(missing);
    }

    [Fact]
    public async Task Turns_down_a_movie_id_that_names_nothing()
    {
        var catalog = new FakeCatalog
        {
            Movie = (_, _) => throw new UpstreamException(AppErrorKind.NotFound, "No such movie.")
        };

        var missing = await TitleCheck.MissingAsync(
            catalog, new TitleTarget(MediaId.Movie(99999999)), TestContext.Current.CancellationToken);

        Assert.Equal(AppErrorKind.NotFound, missing?.Kind);
    }

    [Fact]
    public async Task Turns_down_a_show_id_that_names_nothing()
    {
        var catalog = new FakeCatalog
        {
            Show = (_, _) => throw new UpstreamException(AppErrorKind.NotFound, "No such show.")
        };

        var missing = await TitleCheck.MissingAsync(
            catalog, new TitleTarget(MediaId.TvShow(99999999)), TestContext.Current.CancellationToken);

        Assert.Equal(AppErrorKind.NotFound, missing?.Kind);
    }

    // A catalogue that could not be reached says nothing about the id, and must not be reported
    // as one TMDb has never heard of.
    [Fact]
    public async Task Separates_a_catalogue_it_could_not_reach_from_an_id_that_names_nothing()
    {
        var catalog = new FakeCatalog { Movie = (_, _) => throw new HttpRequestException("offline") };

        var missing = await TitleCheck.MissingAsync(
            catalog, new TitleTarget(MediaId.Movie(603)), TestContext.Current.CancellationToken);

        Assert.Equal(AppErrorKind.Offline, missing?.Kind);
    }

    [Fact]
    public async Task Asks_the_catalogue_for_the_kind_the_target_names()
    {
        var asked = new List<string>();
        var catalog = new FakeCatalog
        {
            Movie = (id, _) =>
            {
                asked.Add($"movie:{id}");
                return Task.FromException<MovieDetails>(new UpstreamException(AppErrorKind.NotFound, "no"));
            },
            Show = (id, _) =>
            {
                asked.Add($"show:{id}");
                return Task.FromException<TvShowDetails>(new UpstreamException(AppErrorKind.NotFound, "no"));
            }
        };

        await TitleCheck.MissingAsync(
            catalog, new TitleTarget(MediaId.Movie(1)), TestContext.Current.CancellationToken);
        await TitleCheck.MissingAsync(
            catalog, new TitleTarget(MediaId.TvShow(2)), TestContext.Current.CancellationToken);

        Assert.Equal(["movie:1", "show:2"], asked);
    }
}
