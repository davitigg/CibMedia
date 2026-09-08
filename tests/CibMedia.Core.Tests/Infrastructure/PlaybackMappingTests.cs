using CibMedia.Core.Common;
using CibMedia.Core.Infrastructure.Playback;
using CibMedia.Playback.Models;
using Xunit;

namespace CibMedia.Core.Tests.Infrastructure;

public sealed class PlaybackMappingTests
{
    [Fact]
    public void Keeps_every_stream_in_api_order_under_its_label()
    {
        var response = Response(Source(
            PlaybackProvider.VideoDb,
            Progressive("Georgian HD", "ka", "ka-hd"),
            Progressive("Georgian SD", "ka", "ka-sd"),
            Progressive("English HD", "EN", "en-hd")));

        var stream = response.ToPlayableStream();

        Assert.Equal(["Georgian HD", "Georgian SD", "English HD"], stream.Source.Streams.Select(entry => entry.Label));
        Assert.Equal("ka-hd", stream.Current.Url);
        Assert.Equal("en", stream.ChooseStream("English HD").Current.Language);
        Assert.False(stream.IsLive);
    }

    [Fact]
    public void Providers_come_in_api_order_and_an_hls_master_names_no_language()
    {
        var response = Response(
            Source(PlaybackProvider.Xpass, Hls("VIP", "m1"), Hls("LUL", "m2")),
            Source(PlaybackProvider.VideoDb, Progressive("Georgian HD", "ka", "ka-hd"), Hls("Auto", "master.m3u8")));

        var stream = response.ToPlayableStream();

        Assert.Equal(["Xpass", "VideoDb"], stream.ProviderNames);
        Assert.Equal(["m1", "m2"], stream.Source.Streams.Select(entry => entry.Url));

        var master = stream.Current;
        Assert.True(master.IsHls);
        Assert.Null(master.Language);

        Assert.Equal(["Georgian HD", "Auto"], stream.ChooseProvider("VideoDb").Source.Streams.Select(entry => entry.Label));
    }

    [Fact]
    public void A_livestream_says_so()
    {
        var response = Response(Source(PlaybackProvider.Liveball, Hls("Channel 1", "c1.m3u8")))
            with { MediaType = PlaybackMediaType.Livestream };

        Assert.True(response.ToPlayableStream().IsLive);
    }

    [Fact]
    public void Headers_and_subtitles_stay_with_their_provider()
    {
        var videoDb = Source(PlaybackProvider.VideoDb, Progressive("Georgian HD", "ka", "ka-hd")) with
        {
            Headers = new Dictionary<string, string> { ["User-Agent"] = "CibMedia" },
            Subtitles = [new PlaybackSubtitle("English", "en", "en.vtt")]
        };

        var xpass = Source(PlaybackProvider.Xpass, Hls("VIP", "m1")) with
        {
            Headers = new Dictionary<string, string> { ["Referer"] = "https://xpass.test" }
        };

        var stream = Response(videoDb, xpass).ToPlayableStream();

        Assert.Equal("CibMedia", stream.Source.Headers["User-Agent"]);
        Assert.Equal("text/vtt", Assert.Single(stream.Source.Subtitles).MimeType);
        Assert.Equal("https://xpass.test", stream.ChooseProvider("Xpass").Source.Headers["Referer"]);
        Assert.Empty(stream.ChooseProvider("Xpass").Source.Subtitles);
    }

    [Fact]
    public void A_provider_with_no_streams_is_left_out_and_none_at_all_is_not_found()
    {
        var stream = Response(Source(PlaybackProvider.Xpass), Source(PlaybackProvider.VideoDb, Hls("Auto", "m")))
            .ToPlayableStream();

        Assert.Equal(["VideoDb"], stream.ProviderNames);

        var error = Assert.Throws<UpstreamException>(() => Response(Source(PlaybackProvider.VideoDb)).ToPlayableStream());
        Assert.Equal(AppErrorKind.NotFound, error.Kind);
    }

    [Fact]
    public void Names_the_providers_in_the_order_they_resolved()
    {
        PlaybackProvider[] providers = [PlaybackProvider.Xpass, PlaybackProvider.VideoDb];

        Assert.Equal(["Xpass", "VideoDb"], providers.ToNames());
    }

    private static HlsStream Hls(string label, string url)
    {
        return new HlsStream(url, label);
    }

    private static ProgressiveStream Progressive(string label, string language, string url)
    {
        return new ProgressiveStream(url, label, "HD", language);
    }

    private static PlaybackSource Source(PlaybackProvider provider, params PlaybackStream[] streams)
    {
        return new PlaybackSource(provider, new Dictionary<string, string>(), streams, []);
    }

    private static ResolvedPlayback Response(params PlaybackSource[] sources)
    {
        return new ResolvedPlayback(PlaybackMediaType.Movie, sources);
    }
}
