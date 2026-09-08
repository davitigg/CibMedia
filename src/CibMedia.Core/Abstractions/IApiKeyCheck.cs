namespace CibMedia.Core.Abstractions;

// Whether the issuing service accepts a candidate key. False only for a definite refusal;
// an unreachable service throws, so the caller can tell the two apart.
public interface IApiKeyCheck
{
    Task<bool> IsValidAsync(ApiKeyKind kind, string apiKey, CancellationToken ct);
}
