namespace CibMedia.Playback.Providers.Xpass.Models;

// Label is a free-form track name, usually a language but also values like "CC", and Language is
// its lowercased form rather than a language code. Only "cached" entries resolve; the rest 404.
// Url is relative to the subtitle host.
internal sealed record XpassSubtitle(string? Label, string? Language, string? Status, string? Url);
