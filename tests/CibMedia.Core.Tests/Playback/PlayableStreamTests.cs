using CibMedia.Core.Playback;
using Xunit;

namespace CibMedia.Core.Tests.Playback;

public sealed class PlayableStreamTests
{
    [Fact]
    public void Choosing_a_provider_by_name_lands_on_its_first_stream()
    {
        var stream = Stream(VideoDb("ka-hd", "ka-sd", "en-hd"), Xpass("VIP", "LUL"));

        var chosen = stream.ChooseProvider("xpass");

        Assert.Equal("Xpass", chosen.Source.Provider);
        Assert.Equal("VIP", chosen.Current.Label);
        Assert.Same(stream, stream.ChooseProvider("Nowhere"));
        Assert.Same(stream, stream.ChooseProvider(null));
    }

    [Fact]
    public void Choosing_a_stream_goes_by_its_label()
    {
        var stream = Stream(VideoDb("ka-hd", "ka-sd", "en-hd", "en-sd"));

        Assert.Equal("en-sd", stream.ChooseStream("EN-SD").Current.Url);
        Assert.Same(stream, stream.ChooseStream("ru-hd"));
    }

    [Fact]
    public void Preferring_dubs_takes_the_first_the_provider_has_and_leaves_a_nameless_stream_alone()
    {
        var stream = Stream(VideoDb("ka-hd", "ka-sd", "en-hd", "en-sd"));

        Assert.Equal("en-hd", stream.PreferLanguages(["de", "en", "ka"]).Current.Url);
        Assert.Same(stream, stream.PreferLanguages(["de"]));
        Assert.Same(stream, stream.PreferLanguages([]));

        var mixed = Stream(Provider("VideoDb", Hls("Auto"), Progressive("en-hd")));

        Assert.Same(mixed, mixed.PreferLanguages(["en"]));
    }

    [Fact]
    public void Fallback_prefers_the_same_language_then_takes_the_rest_in_order()
    {
        var stream = Stream(VideoDb("ka-hd", "ka-sd", "en-hd", "en-sd"));
        var tried = Tried("ka-hd");

        var lower = stream.NextFallback(tried)!;

        Assert.Equal("ka-sd", lower.Current.Url);

        tried.Add("ka-sd");

        Assert.Equal("en-hd", lower.NextFallback(tried)!.Current.Url);
    }

    [Fact]
    public void Fallback_goes_round_and_tries_each_stream_once()
    {
        var stream = Stream(Xpass("VIP", "LUL", "WIS")).ChooseStream("WIS");
        var tried = Tried("WIS");

        var first = stream.NextFallback(tried)!;
        Assert.Equal("VIP", first.Current.Label);

        tried.Add("VIP");
        var second = first.NextFallback(tried)!;
        Assert.Equal("LUL", second.Current.Label);

        tried.Add("LUL");
        Assert.Null(second.NextFallback(tried));
        Assert.Null(Stream(Xpass("VIP")).NextFallback(Tried("VIP")));
    }

    private static PlayableStream Stream(params ProviderStreams[] providers)
    {
        return new PlayableStream(providers);
    }

    private static HashSet<string> Tried(params string[] labels)
    {
        return new HashSet<string>(labels, StringComparer.OrdinalIgnoreCase);
    }

    // "ka-hd" style names: language, then quality. The name is the URL and the label both.
    private static ProviderStreams VideoDb(params string[] streams)
    {
        return Provider("VideoDb", streams.Select(Progressive).ToArray());
    }

    private static ProviderStreams Xpass(params string[] servers)
    {
        return Provider("Xpass", servers.Select(Hls).ToArray());
    }

    private static ProviderStreams Provider(string name, params StreamEntry[] streams)
    {
        return new ProviderStreams(name, new Dictionary<string, string>(), [], streams);
    }

    private static StreamEntry Progressive(string name)
    {
        return new StreamEntry(name, false, name, name[..2]);
    }

    private static StreamEntry Hls(string name)
    {
        return new StreamEntry(name, true, name, null);
    }
}
