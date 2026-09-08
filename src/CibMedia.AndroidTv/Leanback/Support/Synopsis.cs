using Java.Lang;

namespace CibMedia.AndroidTv.Leanback.Support;

// What the details header shows, flattened so one description presenter serves both pages.
public sealed record Synopsis(string Title, ICharSequence? Subtitle, string? Body);
