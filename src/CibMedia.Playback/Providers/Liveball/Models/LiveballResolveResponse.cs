namespace CibMedia.Playback.Providers.Liveball.Models;

// D is base64 of the media URL, M the delivery mode ("h" for an HLS master), E the refusal code.
internal sealed record LiveballResolveResponse(string? D, string? M, string? E);
