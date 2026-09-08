namespace CibMedia.Core.Abstractions;

public static class ApiKeys
{
    // Spelled out rather than Enum.GetValues: that is reflection TrimMode=full strips.
    public static readonly ApiKeyKind[] All = [ApiKeyKind.Tmdb];

    public static bool Has(this IApiKeys keys, ApiKeyKind kind)
    {
        return keys[kind] is { Length: > 0 };
    }

    // The shell is gated on every key being in hand.
    public static bool Complete(this IApiKeys keys)
    {
        return All.All(keys.Has);
    }
}
