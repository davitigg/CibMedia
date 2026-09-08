using CibMedia.Core.Updates;

namespace CibMedia.Core.Abstractions;

// The published manifest, read at launch. Null when it could not be read: a box that cannot
// reach the manifest still starts, on the apk and the config it already has.
public interface IAppUpdates
{
    Task<AppManifest?> ReadAsync(CancellationToken ct);
}
