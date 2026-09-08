namespace CibMedia.Playback.Providers.Xpass;

// PlayerHost is the embed origin, for example play.xpass.top. SubtitleHost belongs to xpass alone: its
// files are timed against xpass encodes and are wrong against anyone else's video.
internal sealed record XpassOptions(bool Enabled, string PlayerHost, string SubtitleHost);