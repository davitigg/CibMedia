namespace CibMedia.Playback.Models;

// A master playlist: variants, audio and subtitle tracks are chosen in the player.
public sealed record HlsStream(string Url, string Label) : PlaybackStream(Url, Label);
