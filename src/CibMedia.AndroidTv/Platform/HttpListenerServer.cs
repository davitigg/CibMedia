using System.Collections.Concurrent;
using System.Net;
using System.Text;
using CibMedia.Core.Abstractions.LocalHttp;

namespace CibMedia.AndroidTv.Platform;

// HttpListener rather than Kestrel: ASP.NET Core publishes no runtime pack for android-arm, so
// a FrameworkReference to Microsoft.AspNetCore.App fails the RID build with NETSDK1082.
// HttpListener is in the base class library and still parses the request line, headers and body.
//
// The socket binds when the first controller is registered and closes behind the last.
public sealed class HttpListenerServer : ILocalHttpServer, IDisposable
{
    // HttpListener cannot bind port 0 and report what the OS gave it, so a busy port walks the
    // next few rather than failing the caller.
    private const int FirstPort = 8730;
    private const int PortsToTry = 5;

    // Nothing served here takes a large body.
    private const int MaxBodyBytes = 8 * 1024;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ConcurrentDictionary<string, LocalHttpHandler> _routes = new();

    private Task? _inFlight;
    private HttpListener? _listener;
    private Task? _loop;

    public string? Address { get; private set; }

    public event Action? AddressChanged;

    // Reached only when the app itself is going. Closing ends the accept loop.
    public void Dispose()
    {
        _listener?.Close();
        _listener = null;
        _gate.Dispose();
    }

    public async Task<IAsyncDisposable> ServeAsync(LocalHttpController controller, CancellationToken ct)
    {
        var keys = controller.Routes.Select(Key).ToArray();

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            foreach (var route in controller.Routes) _routes[Key(route)] = route.Handler;

            // The address is resolved before anything is bound: a box with no network has
            // nothing to publish, and a socket opened first would listen with no way to reach it.
            if (_listener is null && DeviceAddress.LocalIPv4() is { } ip && Listen() is var (listener, port))
            {
                _listener = listener;
                Address = $"http://{ip}:{port}";
                _loop = Task.Run(() => AcceptAsync(listener), CancellationToken.None);
            }
        }
        finally
        {
            _gate.Release();
        }

        // Outside the lock: a handler reacting by registering something else would otherwise
        // wait on a gate its own caller holds.
        if (Address is not null) AddressChanged?.Invoke();

        return new Registration(this, keys);
    }

    private static string Key(LocalHttpRoute route)
    {
        return $"{route.Method} {route.Path}";
    }

    private async Task ReleaseAsync(IEnumerable<string> keys)
    {
        await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            foreach (var key in keys) _routes.TryRemove(key, out _);

            if (!_routes.IsEmpty || _listener is null) return;

            // Whatever is mid-reply finishes before the socket goes: the last controller is
            // usually released because a phone just posted something that closed the screen.
            if (_inFlight is { } inFlight)
                try
                {
                    await inFlight.ConfigureAwait(false);
                }
                catch (Exception error) when (error is IOException or HttpListenerException
                                                  or ObjectDisposedException)
                {
                }

            _listener.Close();

            if (_loop is not null) await _loop.ConfigureAwait(false);

            _listener = null;
            _loop = null;
            _inFlight = null;
            Address = null;
        }
        finally
        {
            _gate.Release();
        }

        if (Address is null) AddressChanged?.Invoke();
    }

    private static (HttpListener Listener, int Port)? Listen()
    {
        for (var port = FirstPort; port < FirstPort + PortsToTry; port++)
        {
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://+:{port}/");

            try
            {
                listener.Start();

                return (listener, port);
            }
            catch (Exception error) when (error is HttpListenerException or PlatformNotSupportedException)
            {
                listener.Close();
            }
        }

        return null;
    }

    // One caller at a time, awaited rather than forked, which is what lets a release wait for
    // the reply in flight.
    private async Task AcceptAsync(HttpListener listener)
    {
        while (listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (Exception error) when (error is HttpListenerException or ObjectDisposedException
                                              or InvalidOperationException)
            {
                return;
            }

            var serving = RespondAsync(context);
            _inFlight = serving;

            try
            {
                await serving.ConfigureAwait(false);
            }
            catch (Exception error) when (error is IOException or HttpListenerException
                                              or ObjectDisposedException)
            {
            }
        }
    }

    private async Task RespondAsync(HttpListenerContext context)
    {
        var path = context.Request.Url?.AbsolutePath ?? "/";
        var method = context.Request.HttpMethod;

        var response = _routes.TryGetValue($"{method} {path}", out var handler)
            ? await handler(
                    new LocalHttpRequest(method, path, await BodyAsync(context.Request).ConfigureAwait(false)),
                    CancellationToken.None)
                .ConfigureAwait(false)
            : LocalHttpResponse.NotFound;

        await WriteAsync(context.Response, response).ConfigureAwait(false);
    }

    private static async Task<string> BodyAsync(HttpListenerRequest request)
    {
        var buffer = new byte[MaxBodyBytes];
        var total = 0;

        while (total < buffer.Length)
        {
            var read = await request.InputStream.ReadAsync(buffer.AsMemory(total)).ConfigureAwait(false);
            if (read == 0) break;

            total += read;
        }

        return (request.ContentEncoding ?? Encoding.UTF8).GetString(buffer, 0, total);
    }

    private static async Task WriteAsync(HttpListenerResponse response, LocalHttpResponse answer)
    {
        var body = Encoding.UTF8.GetBytes(answer.Body);

        response.StatusCode = answer.Status;
        response.ContentType = answer.ContentType;
        response.ContentLength64 = body.Length;
        response.Headers["Cache-Control"] = "no-store";

        await response.OutputStream.WriteAsync(body).ConfigureAwait(false);

        response.Close();
    }

    private sealed class Registration(HttpListenerServer server, IReadOnlyList<string> keys) : IAsyncDisposable
    {
        private bool _released;

        public async ValueTask DisposeAsync()
        {
            if (_released) return;

            _released = true;

            await server.ReleaseAsync(keys).ConfigureAwait(false);
        }
    }
}
