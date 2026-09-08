using CibMedia.Core.Abstractions;

namespace CibMedia.Core.Tests.Fakes;

public sealed class FakeApiKeys : IApiKeys
{
    private readonly Dictionary<ApiKeyKind, string?> _keys = [];

    public string? this[ApiKeyKind kind] => _keys.GetValueOrDefault(kind);

    public void Save(ApiKeyKind kind, string apiKey)
    {
        _keys[kind] = apiKey;
    }
}
