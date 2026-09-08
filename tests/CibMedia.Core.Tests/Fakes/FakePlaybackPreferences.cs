using CibMedia.Core.Abstractions;

namespace CibMedia.Core.Tests.Fakes;

public sealed class FakePlaybackPreferences : IPlaybackPreferences
{
    public string? DefaultProvider { get; set; }

    public string? PreferredLanguage { get; set; }
}
