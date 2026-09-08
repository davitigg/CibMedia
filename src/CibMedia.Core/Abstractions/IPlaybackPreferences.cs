namespace CibMedia.Core.Abstractions;

// What Settings says about how a title starts: the provider when it offers more than one, and
// the dub. Null until someone chooses, which means the first offered and the box's own language.
public interface IPlaybackPreferences
{
    string? DefaultProvider { get; set; }

    string? PreferredLanguage { get; set; }
}
