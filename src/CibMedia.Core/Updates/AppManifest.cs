namespace CibMedia.Core.Updates;

// What a published build says about itself. One document answers both questions the box asks at
// launch: is there a newer apk, and what should playback.json say — so config travels without a
// release and a release is only ever an apk.
public sealed record AppManifest
{
    public required int VersionCode { get; init; }
    public required string VersionName { get; init; }
    public required string ApkUrl { get; init; }

    // Checked against the download before the installer is handed anything.
    public required string Sha256 { get; init; }
    public long SizeBytes { get; init; }
    public string Notes { get; init; } = "";

    // Kept as the text it arrived as: what is written to the box is what was published, not a
    // round-trip through this app's idea of the shape.
    public string? PlaybackJson { get; init; }

    // A release that names no apk, or no checksum for one, describes config and nothing else:
    // there is nothing here that could be installed, so nothing is offered.
    public bool ShouldOffer(int installedVersionCode, int declinedVersionCode)
    {
        return VersionCode > installedVersionCode
               && VersionCode != declinedVersionCode
               && ApkUrl.Length > 0
               && Sha256.Length > 0;
    }

    // What was downloaded is what was published, or it does not reach the installer.
    public bool MatchesChecksum(string hex)
    {
        return Sha256.Length > 0 && string.Equals(Sha256.Trim(), hex.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
