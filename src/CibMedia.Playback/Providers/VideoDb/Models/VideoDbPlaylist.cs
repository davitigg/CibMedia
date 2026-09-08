namespace CibMedia.Playback.Providers.VideoDb.Models;

// Host is the mirror that answered, and its origin is the Referer the CDN accepts.
internal sealed record VideoDbPlaylist(string Host, IReadOnlyList<VideoDbEntry> Entries);
