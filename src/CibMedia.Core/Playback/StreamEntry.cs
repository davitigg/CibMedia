namespace CibMedia.Core.Playback;

// One stream a provider offers, named the way the API names it: a dub and quality, a server, a
// channel. Only a progressive file names its language; an HLS master leaves the dub to the player.
public sealed record StreamEntry(string Url, bool IsHls, string Label, string? Language);
