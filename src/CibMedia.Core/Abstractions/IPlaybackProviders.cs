namespace CibMedia.Core.Abstractions;

// The providers that play TMDb titles, by name, in the API's order of preference. What the
// Preferences screen offers as a default; a title's own list comes with its stream.
public interface IPlaybackProviders
{
    Task<IReadOnlyList<string>> NamesAsync(CancellationToken ct);
}
