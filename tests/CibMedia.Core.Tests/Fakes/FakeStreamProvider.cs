using CibMedia.Core.Abstractions;
using CibMedia.Core.Playback;

namespace CibMedia.Core.Tests.Fakes;

// Resolves everything unless the hook says otherwise, and records what it was asked for.
public sealed class FakeStreamProvider : IStreamProvider
{
    private readonly List<PlaybackTarget> _resolved = [];

    public Func<PlaybackTarget, CancellationToken, Task<PlayableStream>>? Resolve { get; set; }

    public IReadOnlyList<PlaybackTarget> Resolved
    {
        get
        {
            lock (_resolved) return [.. _resolved];
        }
    }

    public Task<PlayableStream> ResolveAsync(PlaybackTarget target, CancellationToken ct)
    {
        lock (_resolved) _resolved.Add(target);

        return Resolve?.Invoke(target, ct)
               ?? Task.FromResult(
                   new PlayableStream(
                   [
                       new ProviderStreams(
                           "VideoDb",
                           new Dictionary<string, string>(),
                           [],
                           [new StreamEntry("https://example.test/stream.m3u8", true, "Stream", null)])
                   ]));
    }
}
