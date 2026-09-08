using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CibMedia.Playback;
using CibMedia.Playback.Models;
using CibMedia.Playback.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

const string Configured = "playback.json";

var services = new ServiceCollection();

services.AddLogging(logging => logging
    .AddSimpleConsole(console => console.SingleLine = true)
    .SetMinimumLevel(LogLevel.Warning)
    .AddFilter("CibMedia.Playback", LogLevel.Debug));

// One in the working directory wins over the one shipped beside the harness, which is how a
// fetched document wins over the one the apk installed.
var configured = File.Exists(Configured)
    ? Path.GetFullPath(Configured)
    : Path.Combine(AppContext.BaseDirectory, Configured);

Console.Error.WriteLine($"using {configured}");

services.AddPlayback(
    JsonSerializer.Deserialize<PlaybackOptions>(File.ReadAllText(configured), JsonSerializerOptions.Web)
    ?? throw new InvalidOperationException($"{configured} is empty."));

await using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();

var tmdb = scope.ServiceProvider.GetRequiredService<TmdbPlaybackService>();
var liveball = scope.ServiceProvider.GetRequiredService<LiveballPlaybackService>();
var providers = scope.ServiceProvider.GetRequiredService<PlaybackProvidersService>();

var output = args switch
{
    ["providers"] =>
        providers.Available.AsJson(),
    ["movie", var id] =>
        (await tmdb.GetMovieAsync(id.AsNumber(), CancellationToken.None)).AsJson(),
    ["tv", var id, var season, var episode] =>
        (await tmdb.GetEpisodeAsync(id.AsNumber(), season.AsNumber(), episode.AsNumber(), CancellationToken.None))
        .AsJson(),
    ["liveball", var url] =>
        (await liveball.GetAsync(url, CancellationToken.None)).AsJson(),
    _ => throw new ArgumentException(
        "usage: providers | movie <tmdbId> | tv <tmdbId> <season> <episode> | liveball <url>")
};

Console.WriteLine(output ?? "no playback");

return output is null ? 1 : 0;

internal static class Arguments
{
    public static int AsNumber(this string value)
    {
        return int.Parse(value, CultureInfo.InvariantCulture);
    }
}

internal static class Answers
{
    // Names rather than numbers, and indented: this output is read, not parsed.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerOptions.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string? AsJson(this ResolvedPlayback? playback)
    {
        return playback is null ? null : JsonSerializer.Serialize(playback, Json);
    }

    public static string AsJson(this IReadOnlyList<PlaybackProvider> providers)
    {
        return JsonSerializer.Serialize(providers, Json);
    }
}
