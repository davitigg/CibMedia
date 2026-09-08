namespace CibMedia.Core.Abstractions;

// The version the box was offered and turned down. Kept so a launch does not ask again about a
// release already refused, and so publishing a newer one asks afresh.
public interface IAppUpdatePreferences
{
    int DeclinedVersionCode { get; set; }
}
