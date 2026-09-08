using CibMedia.Core.Abstractions;

namespace CibMedia.Core.Presentation.Setup;

public sealed record ApiKeysSetupState(string? Address, ApiKeySetupStatus Tmdb)
{
    public static ApiKeysSetupState Initial { get; } = new(null, ApiKeySetupStatus.Waiting);

    public bool Complete => Tmdb is ApiKeySetupStatus.Saved;

    public ApiKeysSetupState With(ApiKeyKind kind, ApiKeySetupStatus status)
    {
        return kind is ApiKeyKind.Tmdb ? this with { Tmdb = status } : this;
    }
}
