namespace CibMedia.Playback.Providers.VideoDb;

// Embed mirrors to race. They front one backend, so any of them is a drop-in replacement when one
// starts failing.
internal sealed record VideoDbOptions(bool Enabled, IReadOnlyList<string> Hosts);