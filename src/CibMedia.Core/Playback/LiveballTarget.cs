namespace CibMedia.Core.Playback;

// A liveball page, handed to the API whole: the box never parses one itself, and nothing in
// the catalogue describes what is behind it.
public sealed record LiveballTarget(string Url) : PlaybackTarget;
