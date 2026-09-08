namespace CibMedia.Core.Abstractions;

// An enum rather than a port per key, so setup, storage and the checks are written once and
// parameterised by which key is in hand.
public enum ApiKeyKind
{
    Tmdb
}
