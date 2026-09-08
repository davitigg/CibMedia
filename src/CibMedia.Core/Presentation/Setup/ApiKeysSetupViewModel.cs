using CibMedia.Core.Abstractions;
using CibMedia.Core.Abstractions.LocalHttp;
using CibMedia.Core.Common;

namespace CibMedia.Core.Presentation.Setup;

// Two keys and two ways in for each, typed on the remote or posted from a phone, all landing
// in SubmitAsync so a key is checked and stored the same way whichever it came from.
public sealed class ApiKeysSetupViewModel : ViewModel<ApiKeysSetupState>
{
    private readonly IApiKeyCheck _check;
    private readonly IApiKeys _keys;
    private readonly ILocalHttpServer _server;
    private readonly CancellationScope _submissions;

    private IAsyncDisposable? _serving;

    public ApiKeysSetupViewModel(IApiKeys keys, IApiKeyCheck check, ILocalHttpServer server)
        : base(ApiKeysSetupState.Initial)
    {
        _keys = keys;
        _check = check;
        _server = server;
        _submissions = new CancellationScope(Lifetime);
    }

    public async Task StartAsync()
    {
        _serving ??= await _server
            .ServeAsync(new SetupController(tmdb => _ = SubmitAsync(tmdb)), Lifetime)
            .ConfigureAwait(false);

        SetState(s => s with { Address = _server.Address is { } address ? address + SetupController.Path : null });
    }

    // For when the screen stops being the one in front, so the code on screen never outlives
    // something to scan it into.
    public async Task StopAsync()
    {
        if (_serving is { } serving)
        {
            _serving = null;

            await serving.DisposeAsync().ConfigureAwait(false);
        }

        SetState(s => s with { Address = null });
    }

    public async Task SubmitAsync(string? tmdb)
    {
        var token = await _submissions.NextAsync().ConfigureAwait(false);

        foreach (var kind in ApiKeys.All) await SaveAsync(kind, tmdb, token).ConfigureAwait(false);
    }

    private async Task SaveAsync(ApiKeyKind kind, string? typed, CancellationToken token)
    {
        var candidate = Normalise(typed);

        // A field left as it was found is not a key to re-check.
        if (candidate is null || candidate == _keys[kind])
        {
            if (_keys.Has(kind)) SetState(s => s.With(kind, ApiKeySetupStatus.Saved));

            return;
        }

        SetState(s => s.With(kind, ApiKeySetupStatus.Checking));

        // No opinion about what a key looks like: only the issuing service can say, and a
        // shape rule of our own turns down a valid key the day a format changes.
        var verdict = await Load.RunAsync(c => _check.IsValidAsync(kind, candidate, c), token)
            .ConfigureAwait(false);

        if (token.IsCancellationRequested) return;

        switch (verdict)
        {
            case Load<bool>.Ready { Value: true }:
                _keys.Save(kind, candidate);
                SetState(s => s.With(kind, ApiKeySetupStatus.Saved));
                break;
            case Load<bool>.Ready:
                SetState(s => s.With(kind, ApiKeySetupStatus.Rejected));
                break;
            default:
                SetState(s => s.With(kind, ApiKeySetupStatus.Unreachable));
                break;
        }
    }

    // Not awaited: disposal is synchronous from the caller's side and releasing a route only
    // has an accept loop to unwind.
    protected override void OnDispose()
    {
        _ = _serving?.DisposeAsync().AsTask();
        _serving = null;
        _submissions.Dispose();
    }

    // A phone's clipboard usually carries trailing whitespace.
    private static string? Normalise(string? key)
    {
        return string.IsNullOrWhiteSpace(key) ? null : key.Trim();
    }
}
