namespace CibMedia.Core.Abstractions;

// What the resolvers are holding: answers, and the build ids and keys those were resolved with.
// Cleared from Settings alongside the stores the app keeps itself.
public interface IPlaybackCache
{
    void Clear();
}
