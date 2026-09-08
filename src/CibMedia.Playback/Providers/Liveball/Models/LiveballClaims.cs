namespace CibMedia.Playback.Providers.Liveball.Models;

// The claim set at the front of a resolve token. Ch names the channel it was minted for.
internal sealed record LiveballClaims(string? Ch);
