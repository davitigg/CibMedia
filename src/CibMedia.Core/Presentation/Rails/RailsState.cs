using CibMedia.Core.Catalog;
using CibMedia.Core.Common;

namespace CibMedia.Core.Presentation.Rails;

// A slot per rail rather than a property per rail: the catalogue pages decide their rails
// from a table.
public sealed record RailsState<TKey>(IReadOnlyDictionary<TKey, Load<IReadOnlyList<MediaCard>>> Rails)
    where TKey : notnull
{
    public static RailsState<TKey> Loading(IEnumerable<TKey> keys)
    {
        return new RailsState<TKey>(
            keys.ToDictionary(key => key, _ => Load.Loading<IReadOnlyList<MediaCard>>()));
    }

    public Load<IReadOnlyList<MediaCard>> For(TKey key)
    {
        return Rails.TryGetValue(key, out var load) ? load : Load.Loading<IReadOnlyList<MediaCard>>();
    }

    // Itself when the rail already holds this exact result, so it does not travel any further
    // as a state change.
    public RailsState<TKey> With(TKey key, Load<IReadOnlyList<MediaCard>> load)
    {
        if (Rails.TryGetValue(key, out var existing) && ReferenceEquals(existing, load)) return this;

        return new RailsState<TKey>(
            new Dictionary<TKey, Load<IReadOnlyList<MediaCard>>>(Rails) { [key] = load });
    }
}
