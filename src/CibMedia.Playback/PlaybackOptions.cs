namespace CibMedia.Playback;

// Where a build is pointed, in the shape of playback.json. The host reads that file; a fetched
// document replaces it whole, so there is one shape and one place it comes from.
public sealed record PlaybackOptions
{
    public VideoDbSettings VideoDb { get; init; } = new();

    public XpassSettings Xpass { get; init; } = new();

    public LiveballSettings Liveball { get; init; } = new();

    public sealed record VideoDbSettings
    {
        public bool Enabled { get; init; }

        // Mirrors of one catalogue, raced in order.
        public IReadOnlyList<string> Hosts { get; init; } = [];
    }

    public sealed record XpassSettings
    {
        public bool Enabled { get; init; }

        public string PlayerHost { get; init; } = "";

        public string SubtitleHost { get; init; } = "";
    }

    public sealed record LiveballSettings
    {
        public bool Enabled { get; init; }

        // Hosts a play command may name; anything else is refused before it is fetched.
        public IReadOnlyList<string> PageHosts { get; init; } = [];

        // Read together when the key is recovered: one page rarely pins it down alone.
        public IReadOnlyList<string> KeyPages { get; init; } = [];
    }
}
