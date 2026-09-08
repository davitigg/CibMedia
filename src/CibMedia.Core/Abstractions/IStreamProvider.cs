using CibMedia.Core.Playback;

namespace CibMedia.Core.Abstractions;

public interface IStreamProvider
{
    // May answer from a previous resolve, for as long as the resolver holds one.
    Task<PlayableStream> ResolveAsync(PlaybackTarget target, CancellationToken ct);
}
