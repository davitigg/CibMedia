namespace CibMedia.Playback.Providers;

// A budget this side set, told apart from the upstream falling over. Both arrive as a cancellation
// nobody asked for, and only the second is evidence about the stack: the first says one lookup ran
// long, which is no reason to skip every other title.
internal sealed class PlaybackTimeoutException(string what, Exception cancellation)
    : Exception($"{what} did not answer inside its budget.", cancellation);
