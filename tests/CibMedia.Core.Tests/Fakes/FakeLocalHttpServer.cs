using CibMedia.Core.Abstractions.LocalHttp;

namespace CibMedia.Core.Tests.Fakes;

public sealed class FakeLocalHttpServer : ILocalHttpServer
{
    private readonly Dictionary<string, LocalHttpHandler> _routes = [];

    public int Served { get; private set; }

    public int Released { get; private set; }

    public string? Address => _routes.Count > 0 ? "http://192.168.0.9:8730" : null;

    public event Action? AddressChanged;

    public Task<IAsyncDisposable> ServeAsync(LocalHttpController controller, CancellationToken ct)
    {
        foreach (var route in controller.Routes) _routes[Key(route.Method, route.Path)] = route.Handler;

        Served++;
        AddressChanged?.Invoke();

        return Task.FromResult<IAsyncDisposable>(
            new Registration(this, controller.Routes.Select(r => Key(r.Method, r.Path)).ToArray()));
    }

    public Task<LocalHttpResponse> GetAsync(string path)
    {
        return SendAsync("GET", path, string.Empty);
    }

    public Task<LocalHttpResponse> PostAsync(string path, string body)
    {
        return SendAsync("POST", path, body);
    }

    public Task<LocalHttpResponse> SendAsync(string method, string path, string body)
    {
        return _routes.TryGetValue(Key(method, path), out var handler)
            ? handler(new LocalHttpRequest(method, path, body), CancellationToken.None)
            : Task.FromResult(LocalHttpResponse.NotFound);
    }

    private static string Key(string method, string path)
    {
        return $"{method} {path}";
    }

    private sealed class Registration(FakeLocalHttpServer server, IReadOnlyList<string> keys) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            foreach (var key in keys) server._routes.Remove(key);

            server.Released++;
            server.AddressChanged?.Invoke();

            return ValueTask.CompletedTask;
        }
    }
}
