using CibMedia.Playback.Models;

namespace CibMedia.Playback.Providers;

// Distinct from an upstream fault so an open window is reported once when it opens rather than
// once per request for its length.
internal sealed class PlaybackOutageException(PlaybackProvider provider)
    : Exception($"{provider} is inside an outage window.")
{
    public PlaybackProvider Provider { get; } = provider;
}