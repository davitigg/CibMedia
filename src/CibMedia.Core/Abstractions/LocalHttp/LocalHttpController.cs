namespace CibMedia.Core.Abstractions.LocalHttp;

// A group of routes registered and released together. Declared in code rather than found by
// attributes: attribute routing is reflection TrimMode=full strips.
public abstract class LocalHttpController
{
    private readonly List<LocalHttpRoute> _routes = [];

    public IReadOnlyList<LocalHttpRoute> Routes => _routes;

    protected void Get(string path, LocalHttpHandler handler)
    {
        _routes.Add(new LocalHttpRoute("GET", path, handler));
    }

    protected void Post(string path, LocalHttpHandler handler)
    {
        _routes.Add(new LocalHttpRoute("POST", path, handler));
    }
}
