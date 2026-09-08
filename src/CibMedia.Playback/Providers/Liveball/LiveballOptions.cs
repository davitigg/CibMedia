namespace CibMedia.Playback.Providers.Liveball;

// KeyPages are the channel pages, which carry video most of the time; their payloads seed token key
// recovery.
internal sealed record LiveballOptions(bool Enabled, IReadOnlyList<string> PageHosts, IReadOnlyList<Uri> KeyPages);