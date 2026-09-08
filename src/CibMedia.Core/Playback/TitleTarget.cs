using CibMedia.Core.Catalog;

namespace CibMedia.Core.Playback;

// Episode fields are null for a movie.
public sealed record TitleTarget(MediaId Id, int? SeasonNumber = null, int? EpisodeNumber = null) : PlaybackTarget;
