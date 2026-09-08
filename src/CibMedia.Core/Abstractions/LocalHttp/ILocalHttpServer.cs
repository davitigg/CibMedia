namespace CibMedia.Core.Abstractions.LocalHttp;

// The box answering HTTP on the LAN, so a phone can do what a D-pad is bad at. The port is
// bound while at least one controller is registered and released behind the last.
public interface ILocalHttpServer
{
    // "http://host:port" with no trailing slash. Null while nothing is served, or when the box
    // has no address to serve on.
    string? Address { get; }

    event Action? AddressChanged;

    // Answers the controller's routes until the returned handle is disposed.
    Task<IAsyncDisposable> ServeAsync(LocalHttpController controller, CancellationToken ct);
}
