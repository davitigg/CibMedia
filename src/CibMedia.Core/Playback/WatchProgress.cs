using CibMedia.Core.Catalog;

namespace CibMedia.Core.Playback;

// Keyed by title, not by episode: a series holds one entry that tracks where you are.
public sealed record WatchProgress(
    MediaId Id,
    string Title,
    string? PosterUrl,
    string? BackdropUrl,
    int? Year,
    double? Rating,
    int? SeasonNumber,
    int? EpisodeNumber,
    long PositionMs,
    long DurationMs,
    DateTimeOffset WatchedAt)
{
    // A resume backs up far enough to carry a line of dialogue.
    private static readonly TimeSpan Rewind = TimeSpan.FromSeconds(10);

    public double Fraction => DurationMs > 0 ? Math.Clamp((double)PositionMs / DurationMs, 0, 1) : 0;

    public long ResumeFromMs => Math.Max(0, PositionMs - (long)Rewind.TotalMilliseconds);

    // Zero when the duration was never known, so a caller can tell "nothing to say" from
    // "nothing left".
    public long RemainingMs => DurationMs > 0 ? Math.Max(0, DurationMs - PositionMs) : 0;

    // Well before the credits: a title parked at 96% forever is worse than clearing it early.
    public bool IsFinished => Fraction >= 0.9;

    // A stored position belongs to one episode; any other target starts from the beginning.
    public long ResumeMsFor(TitleTarget target)
    {
        return target.Id == Id
               && target.SeasonNumber == SeasonNumber
               && target.EpisodeNumber == EpisodeNumber
            ? ResumeFromMs
            : 0;
    }

    public TitleTarget ToTarget()
    {
        return new TitleTarget(Id, SeasonNumber, EpisodeNumber);
    }
}
