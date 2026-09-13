using AndroidX.Media3.Common;
using AndroidX.Media3.ExoPlayer;

namespace CibMedia.AndroidTv.Playback;

// The subtitle the viewer picked, held by name rather than by the track it names. Media3 stores a
// pick as an override onto a TrackGroup belonging to the player that was up when it was made, and
// the player is released every time the box leaves the screen, so nothing about the pick itself
// survives; the label and language do, and they find it again on the player that comes back.
public sealed record SubtitleChoice(string? Language, string? Label)
{
    // Null when the viewer is watching without subtitles, which is as much an answer as a track is,
    // and when there is no player to ask.
    public static SubtitleChoice? Of(IExoPlayer? player)
    {
        if (player?.CurrentTracks?.Groups is not { } groups) return null;

        foreach (var entry in groups)
        {
            if (entry is not Tracks.Group group || group.Type != C.TrackTypeText) continue;

            for (var track = 0; track < group.Length; track++)
            {
                if (!group.IsTrackSelected(track)) continue;

                var format = group.GetTrackFormat(track);

                return new SubtitleChoice(format?.Language, format?.Label);
            }
        }

        return null;
    }

    // False until the track turns up. The caller keeps asking: an HLS stream declares its text
    // tracks as it reads them, so the one being looked for can arrive a beat after the video.
    public bool ApplyTo(IExoPlayer player, Tracks? tracks)
    {
        if (tracks?.Groups is not { } groups) return false;

        foreach (var entry in groups)
        {
            if (entry is not Tracks.Group group || group.Type != C.TrackTypeText) continue;

            for (var track = 0; track < group.Length; track++)
            {
                if (!Names(group.GetTrackFormat(track))) continue;

                player.TrackSelectionParameters = player.TrackSelectionParameters!
                    .BuildUpon()!
                    .SetTrackTypeDisabled(C.TrackTypeText, false)!
                    .SetOverrideForType(new TrackSelectionOverride(group.MediaTrackGroup!, track))!
                    .Build()!;

                return true;
            }
        }

        return false;
    }

    // Both, because one is not enough: upstream ships the same language twice often enough to
    // carry an index on the duplicate, and a playlist's own text track can share a language with
    // one the API side-loaded.
    private bool Names(Format? format)
    {
        return format is not null
               && Same(format.Language, Language)
               && Same(format.Label, Label);
    }

    private static bool Same(string? left, string? right)
    {
        return string.Equals(left ?? "", right ?? "", StringComparison.OrdinalIgnoreCase);
    }
}
