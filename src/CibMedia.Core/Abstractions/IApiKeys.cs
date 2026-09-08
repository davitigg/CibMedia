namespace CibMedia.Core.Abstractions;

public interface IApiKeys
{
    string? this[ApiKeyKind kind] { get; }

    void Save(ApiKeyKind kind, string apiKey);
}
