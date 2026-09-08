namespace CibMedia.Playback.Models;

// Language is BCP-47 when the label maps to one; where it does not, Label is what a picker shows.
public sealed record PlaybackSubtitle(string Label, string? Language, string Url);
