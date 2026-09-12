using System.Net;
using System.Text;
using CibMedia.Playback.Providers.Xpass;
using CibMedia.Playback.Providers.Xpass.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CibMedia.Core.Tests.Playback;

public sealed class XpassStreamResolverTests
{
    private const string Origin = "https://play.example/";

    private const int WaveSize = 4;

    [Fact]
    public async Task Stops_at_the_first_wave_that_carries_enough()
    {
        var upstream = new FakeUpstream();
        upstream.Plays("VIP 1");
        upstream.Plays("WIS 1");
        upstream.PlaysWrapped("TIK 1");
        upstream.Plays("LUL 7");

        var streams = await Resolve(upstream, Named("VIP 1", "WIS 1", "TIK 1", "LUL 1", "LUL 7"));

        Assert.Equal(["VIP 1", "WIS 1", "TIK 1"], streams.Select(stream => stream.Label));
        Assert.DoesNotContain("LUL 7", upstream.Asked);
    }

    // A wave answers as a whole, so the fourth that verified alongside them is not offered.
    [Fact]
    public async Task Offers_no_more_than_it_stopped_at()
    {
        var upstream = new FakeUpstream();
        upstream.Plays("VIP 1");
        upstream.Plays("WIS 1");
        upstream.Plays("MOL 1");
        upstream.Plays("LUL 1");

        var streams = await Resolve(upstream, Named("VIP 1", "WIS 1", "MOL 1", "LUL 1"));

        Assert.Equal(3, streams.Count);
    }

    [Fact]
    public async Task Reaches_a_later_wave_when_the_first_carries_nothing()
    {
        var upstream = new FakeUpstream();
        upstream.Plays("LUL 7");
        upstream.Plays("MOL 4");

        var streams = await Resolve(
            upstream,
            Named("LUL 1", "LUL 2", "LUL 3", "LUL 4", "LUL 5", "LUL 6", "LUL 7", "MOL 4"));

        Assert.Equal(["LUL 7", "MOL 4"], streams.Select(stream => stream.Label).Order());
        Assert.Contains("LUL 7", upstream.Asked);
    }

    // Eight LUL entries would otherwise spend the whole wave on one family's guess.
    [Fact]
    public async Task Caps_a_family_inside_one_wave()
    {
        var upstream = new FakeUpstream();
        upstream.Plays("VIP 1");
        upstream.Plays("MOL 1");

        var streams = await Resolve(
            upstream,
            Named("LUL 1", "LUL 2", "LUL 3", "LUL 4", "LUL 5", "VIP 1", "MOL 1"));

        Assert.Equal(["MOL 1", "VIP 1"], streams.Select(stream => stream.Label).Order());
        Assert.Equal(["LUL 1", "LUL 2", "VIP 1", "MOL 1"], upstream.Asked.Take(WaveSize));
    }

    // The head strips the wrapper, so a wrapped segment counts as carried and its server takes the
    // place its family earned. GLE is wrapped too but was never measured, so it ranks behind both.
    [Fact]
    public async Task Carries_a_png_wrapped_segment()
    {
        var upstream = new FakeUpstream();
        upstream.PlaysWrapped("TIK 1");
        upstream.PlaysWrapped("GLE 1");
        upstream.Plays("VIP 1");

        var streams = await Resolve(upstream, Named("TIK 1", "GLE 1", "VIP 1"));

        Assert.Equal(["TIK 1", "VIP 1", "GLE 1"], streams.Select(stream => stream.Label));
    }

    [Fact]
    public async Task Carries_nothing_from_a_png_that_closes_on_no_transport_stream()
    {
        var upstream = new FakeUpstream();
        upstream.PlaysWrapped("TIK 1", carriesTransportStream: false);

        var streams = await Resolve(upstream, Named("TIK 1"));

        Assert.Empty(streams);
    }

    // The sentinel a server answers with for a title it does not hold: no master, no hops chasing one.
    [Fact]
    public async Task Spends_no_hop_on_a_server_that_answers_with_the_missing_file()
    {
        var upstream = new FakeUpstream();
        upstream.Missing("VIP 1");
        upstream.Plays("WIS 1");

        var streams = await Resolve(upstream, Named("VIP 1", "WIS 1"));

        Assert.Equal(["WIS 1"], streams.Select(stream => stream.Label));
        Assert.DoesNotContain(upstream.Fetched, url => url.Contains("VIP 1/master", StringComparison.Ordinal));
    }

    private static async Task<IReadOnlyList<CibMedia.Playback.Models.PlaybackStream>> Resolve(
        FakeUpstream upstream,
        IReadOnlyList<XpassServer> servers
    )
    {
        var resolver = new XpassStreamResolver(
            new HttpClient(upstream),
            new XpassOptions(true, "play.example", "sub.example"),
            NullLogger<XpassStreamResolver>.Instance);

        return await resolver.ProbeAsync(servers, TestContext.Current.CancellationToken);
    }

    private static List<XpassServer> Named(params string[] names)
    {
        return names.Select(name => new XpassServer(name, $"/playlist/{name}")).ToList();
    }

    // Answers the four hops a probe walks. A server plays only when it was told to; everything
    // else 404s, which is what a server holding no encode for the episode does.
    private sealed class FakeUpstream : HttpMessageHandler
    {
        private readonly HashSet<string> _playing = [];
        private readonly HashSet<string> _missing = [];
        private readonly HashSet<string> _wrapped = [];
        private readonly HashSet<string> _hollow = [];
        private readonly List<string> _asked = [];
        private readonly List<string> _fetched = [];

        public IReadOnlyList<string> Asked => _asked;

        public IReadOnlyList<string> Fetched => _fetched;

        public void Plays(string server) => _playing.Add(server);

        public void PlaysWrapped(string server, bool carriesTransportStream = true)
        {
            _playing.Add(server);
            _wrapped.Add(server);

            if (!carriesTransportStream) _hollow.Add(server);
        }

        public void Missing(string server) => _missing.Add(server);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var url = Uri.UnescapeDataString(request.RequestUri!.ToString());

            lock (_fetched) _fetched.Add(url);

            if (url.StartsWith($"{Origin}playlist/", StringComparison.Ordinal))
            {
                var server = url[$"{Origin}playlist/".Length..];

                lock (_asked) _asked.Add(server);

                if (_missing.Contains(server)) return Text($@"[{{""sources"":[{{""file"":""/video/error""}}]}}]");
                if (!_playing.Contains(server)) return Status(HttpStatusCode.NotFound);

                return Text($@"[{{""sources"":[{{""file"":""https://cdn.example/{server}/master.m3u8""}}]}}]");
            }

            if (url.EndsWith("master.m3u8", StringComparison.Ordinal))
                return Text("#EXTM3U\n#EXTINF:6,\nsegment.ts\n");

            if (url.EndsWith("segment.ts", StringComparison.Ordinal))
            {
                var server = url["https://cdn.example/".Length..].Split('/')[0];

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.PartialContent)
                {
                    Content = new ByteArrayContent(Segment(_wrapped.Contains(server), _hollow.Contains(server)))
                });
            }

            return Status(HttpStatusCode.NotFound);
        }

        // A wrapped segment is the transport stream appended to a complete 1x1 PNG, prefix length
        // and all; a hollow one is the same PNG with nothing behind it.
        private static byte[] Segment(bool wrapped, bool hollow)
        {
            byte[] png =
            [
                0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
                0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
                0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
                0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
            ];

            var packets = new byte[377];
            packets[0] = 0x47;
            packets[188] = 0x47;
            packets[376] = 0x47;

            if (hollow) return png;

            return wrapped ? [.. png, .. packets] : packets;
        }

        private static Task<HttpResponseMessage> Text(string body)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8)
            });
        }

        private static Task<HttpResponseMessage> Status(HttpStatusCode code)
        {
            return Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent("") });
        }
    }
}
