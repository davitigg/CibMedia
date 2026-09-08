using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using CibMedia.Core.Abstractions.LocalHttp;
using CibMedia.Core.Catalog;
using CibMedia.Core.Playback;

namespace CibMedia.Core.Presentation.Remote;

// Being told what to put on, from a phone on the same network. A title opens rather than
// plays: it takes the TV to its details page and stops there, so nobody can start something
// on a TV that someone else is watching. A liveball url has no page to stop at and plays.
public sealed class PlaybackController : LocalHttpController
{
    public const string Prefix = "/playback";

    public PlaybackController()
    {
        Post($"{Prefix}/movie", (request, _) => Task.FromResult(Open(
            request,
            PlaybackCommandJson.Default.PlayMovieCommand,
            command => new TitleTarget(MediaId.Movie(command.TmdbId)))));

        Post($"{Prefix}/tv-show", (request, _) => Task.FromResult(Open(
            request,
            PlaybackCommandJson.Default.PlayTvShowCommand,
            command => new TitleTarget(
                MediaId.TvShow(command.TmdbId),
                command.Season,
                command.Episode))));

        Post($"{Prefix}/liveball", (request, _) => Task.FromResult(Play(request)));
    }

    public event Action<TitleTarget>? OpenRequested;

    public event Action<LiveballTarget>? PlayRequested;

    private static TCommand? Read<TCommand>(LocalHttpRequest request, JsonTypeInfo<TCommand> shape)
        where TCommand : class
    {
        try
        {
            return JsonSerializer.Deserialize(request.Body, shape);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private LocalHttpResponse Open<TCommand>(
        LocalHttpRequest request,
        JsonTypeInfo<TCommand> shape,
        Func<TCommand, TitleTarget> target)
        where TCommand : class
    {
        if (Read(request, shape) is not { } command) return LocalHttpResponse.BadRequest;

        OpenRequested?.Invoke(target(command));

        return LocalHttpResponse.Accepted;
    }

    private LocalHttpResponse Play(LocalHttpRequest request)
    {
        if (Read(request, PlaybackCommandJson.Default.PlayLiveballCommand) is not { Url: { } url }
            || PageUrl(url) is not { } page)
            return LocalHttpResponse.BadRequest;

        PlayRequested?.Invoke(new LiveballTarget(page));

        return LocalHttpResponse.Accepted;
    }

    // Turned down here rather than on the TV: the sender is the one that can fix a malformed url,
    // and it is still holding the connection. Which hosts the API will fetch is the API's to say,
    // so this checks the shape and nothing else.
    private static string? PageUrl(string url)
    {
        return Uri.TryCreate(url.Trim(), UriKind.Absolute, out var page)
               && page.Scheme is "http" or "https"
            ? page.ToString()
            : null;
    }
}
