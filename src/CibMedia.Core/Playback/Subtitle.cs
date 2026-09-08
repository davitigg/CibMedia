namespace CibMedia.Core.Playback;

public sealed record Subtitle(string Label, string Url, string? MimeType = null, string? Language = null);
