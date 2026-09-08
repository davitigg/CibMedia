namespace CibMedia.Playback.Providers.Xpass.Models;

// Name is a free-form label such as "TIK 1" or "LUL 2"; Url is a playlist endpoint relative to the
// player origin.
internal sealed record XpassServer(string? Name, string? Url);
