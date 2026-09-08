namespace CibMedia.Core.Playback;

// What the player is pointed at. A target is resolved to streams and nothing else reads it,
// so the two kinds share no fields.
public abstract record PlaybackTarget;
