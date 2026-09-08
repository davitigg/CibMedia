using CibMedia.Core.Abstractions;
using CibMedia.Core.Common;
using CibMedia.Core.Playback;

namespace CibMedia.Core.Presentation.Playback;

// The providers a target plays from, by resolving it. There is no cheaper question: the
// resolve is the only per-title playback endpoint. A NotFound failure is the one answer that
// means "nothing to play"; offline, a timeout or a bad minute at the backend fail as themselves
// and read as not known yet.
public static class PlayCheck
{
    public static async Task<Load<IReadOnlyList<string>>> RunAsync(
        IStreamProvider streams, PlaybackTarget target, CancellationToken ct)
    {
        return await Load.RunAsync(c => streams.ResolveAsync(target, c), ct).ConfigureAwait(false) switch
        {
            Load<PlayableStream>.Ready ready => Load.Ready(ready.Value.ProviderNames),
            Load<PlayableStream>.Failed failed => Load.Failed<IReadOnlyList<string>>(failed.Error),
            _ => Load.Loading<IReadOnlyList<string>>()
        };
    }
}
