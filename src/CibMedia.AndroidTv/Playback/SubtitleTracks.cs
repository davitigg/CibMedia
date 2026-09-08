using AndroidX.Media3.Common;
using CibMedia.Core.Playback;

namespace CibMedia.AndroidTv.Playback;

public static class SubtitleTracks
{
    // Entries without a known MIME type are dropped: ExoPlayer needs the format up front, and
    // a subtitle it cannot identify fails to load silently.
    public static List<MediaItem.SubtitleConfiguration> For(IReadOnlyList<Subtitle> subtitles)
    {
        return
        [
            .. subtitles
                .Where(subtitle => subtitle.MimeType is not null)
                .Select(subtitle =>
                    new MediaItem.SubtitleConfiguration.Builder(Android.Net.Uri.Parse(subtitle.Url))!
                        .SetMimeType(subtitle.MimeType)!
                        .SetLabel(subtitle.Label)!
                        .SetLanguage(subtitle.Language)!
                        .Build()!)
        ];
    }
}
