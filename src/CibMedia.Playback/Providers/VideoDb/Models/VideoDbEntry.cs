namespace CibMedia.Playback.Providers.VideoDb.Models;

// A node in the playlist response: a movie is one entry, a serial one entry per season, each
// carrying its episodes in Folder.
internal sealed record VideoDbEntry
{
    // An HLS master URL or the packed direct-file string, and absent on season nodes.
    public string? File { get; init; }

    // "{season}-{episode}" on episode nodes.
    public string? Id { get; init; }

    public string? Title { get; init; }

    public IReadOnlyList<VideoDbEntry>? Folder { get; init; }

    public IReadOnlyList<VideoDbSubtitle>? Subtitles { get; init; }
}
