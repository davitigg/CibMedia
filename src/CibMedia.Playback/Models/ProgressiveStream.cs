namespace CibMedia.Playback.Models;

// One file carrying one audio track, so another language is another stream. Quality is the
// provider's own tag ("HD"); Language is BCP-47.
public sealed record ProgressiveStream(string Url, string Label, string? Quality, string? Language)
    : PlaybackStream(Url, Label);
