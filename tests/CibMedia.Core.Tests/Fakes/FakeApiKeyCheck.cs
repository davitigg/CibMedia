using CibMedia.Core.Abstractions;

namespace CibMedia.Core.Tests.Fakes;

public sealed class FakeApiKeyCheck : IApiKeyCheck
{
    public List<(ApiKeyKind Kind, string Key)> Asked { get; } = [];

    public bool Accepts { get; set; } = true;

    // A box that cannot reach the service at all.
    public Exception? Throws { get; set; }

    // Held open to keep a check running.
    public Task? Gate { get; set; }

    public async Task<bool> IsValidAsync(ApiKeyKind kind, string apiKey, CancellationToken ct)
    {
        Asked.Add((kind, apiKey));

        if (Gate is { } gate) await gate;

        return Throws is not null ? throw Throws : Accepts;
    }
}
