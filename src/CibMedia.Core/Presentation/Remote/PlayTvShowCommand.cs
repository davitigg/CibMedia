namespace CibMedia.Core.Presentation.Remote;

// Season and episode are null for "open the series wherever it should open".
public sealed record PlayTvShowCommand(int TmdbId, int? Season, int? Episode);
