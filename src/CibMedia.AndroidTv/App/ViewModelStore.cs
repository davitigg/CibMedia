namespace CibMedia.AndroidTv.App;

// Leanback builds a section's fragment fresh every time the section is selected; without
// somewhere outside the fragment to keep it, the view model and everything it loaded would go
// with it. Keyed by page rather than by type, because Movies and TV Shows share a class.
public sealed class ViewModelStore
{
    private readonly Dictionary<string, IDisposable> _models = [];

    // reused tells the caller it has a page that is already filled, which the fragment cannot
    // tell on its own: the fragment is always new.
    public T GetOrCreate<T>(string key, Func<T> create, out bool reused)
        where T : class, IDisposable
    {
        if (_models.TryGetValue(key, out var existing))
        {
            reused = true;
            return (T)existing;
        }

        reused = false;

        var created = create();
        _models[key] = created;

        return created;
    }
}
