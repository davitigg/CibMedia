namespace CibMedia.AndroidTv.App;

public enum UpdateCheckResult
{
    // Asked again inside the interval, so nothing was fetched and nothing is known.
    Skipped,
    Unreachable,

    // Read, and carrying config even though it names no apk worth offering.
    UpToDate,
    Offered
}
