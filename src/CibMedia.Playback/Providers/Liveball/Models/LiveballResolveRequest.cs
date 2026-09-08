namespace CibMedia.Playback.Providers.Liveball.Models;

// The resolve call's wire shape, single-letter names included. F is a client fingerprint.
internal sealed record LiveballResolveRequest(string T, string F);
