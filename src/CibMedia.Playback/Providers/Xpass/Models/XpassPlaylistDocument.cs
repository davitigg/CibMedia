namespace CibMedia.Playback.Providers.Xpass.Models;

// A server answers either with a bare item array or with it wrapped under "playlist"; the embed
// accepts both (`pl.playlist || pl`).
internal sealed record XpassPlaylistDocument(IReadOnlyList<XpassPlaylistItem>? Playlist);
