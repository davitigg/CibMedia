namespace CibMedia.Playback.Providers.Xpass.Models;

// Type is "hls" or "mp4". Label is the quality on MP4 servers ("720p") and the server name otherwise.
internal sealed record XpassSource(string? File, string? Type, string? Label);
