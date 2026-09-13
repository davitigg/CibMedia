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

    private const int InFlight = 4;

    [Fact]
    public async Task Starts_nothing_behind_the_answer_it_already_has()
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

    // The pool runs one probe more than the answer needs, so a fourth can verify alongside the
    // three that fill it.
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
    public async Task Reaches_the_candidates_behind_a_pool_that_carried_nothing()
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

    // Eight LUL entries would otherwise hold every slot on one family's guess.
    [Fact]
    public async Task Caps_a_family_in_flight()
    {
        var upstream = new FakeUpstream();
        upstream.Plays("VIP 1");
        upstream.Plays("MOL 1");

        var streams = await Resolve(
            upstream,
            Named("LUL 1", "LUL 2", "LUL 3", "LUL 4", "LUL 5", "VIP 1", "MOL 1"));

        Assert.Equal(["MOL 1", "VIP 1"], streams.Select(stream => stream.Label).Order());
        Assert.Equal(["LUL 1", "LUL 2", "VIP 1", "MOL 1"], upstream.Asked.Take(InFlight));
    }

    // The slot a probe holds is its own: a candidate still waiting holds neither the ones started
    // beside it nor the ones queued behind it.
    [Fact]
    public async Task Fills_the_slot_a_finished_probe_leaves_while_another_is_still_waiting()
    {
        var upstream = new FakeUpstream();
        upstream.Holds("VIP 1");
        upstream.Plays("MOL 1");

        var probe = Resolve(upstream, Named("VIP 1", "WIS 1", "TIK 1", "LUL 1", "MOL 1"));

        await upstream.Reached("MOL 1");
        upstream.Release();

        var streams = await probe;

        Assert.Equal(["MOL 1"], streams.Select(stream => stream.Label));
    }

    // Probes land in whatever order the hosts answer; what is offered is the order they ranked in,
    // because the first stream in the list is the one the player starts on.
    [Fact]
    public async Task Offers_the_streams_in_ranked_order_however_they_landed()
    {
        var upstream = new FakeUpstream();
        upstream.Holds("VIP 1");
        upstream.Plays("VIP 1");
        upstream.Plays("MOL 1");

        var probe = Resolve(upstream, Named("VIP 1", "MOL 1"));

        await upstream.Reached("MOL 1");
        upstream.Release();

        var streams = await probe;

        Assert.Equal(["VIP 1", "MOL 1"], streams.Select(stream => stream.Label));
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
        private readonly HashSet<string> _held = [];
        private readonly List<string> _asked = [];
        private readonly List<string> _fetched = [];
        private readonly TaskCompletionSource _release = new();

        private readonly Dictionary<string, TaskCompletionSource> _reached =
            new(StringComparer.Ordinal);

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

        // Answers nothing until Release, which is the shape of a host that is reachable but slow.
        public void Holds(string server) => _held.Add(server);

        public void Release() => _release.TrySetResult();

        public Task Reached(string server)
        {
            lock (_reached) return Slot(server).Task;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
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
                lock (_reached) Slot(server).TrySetResult();

                if (_held.Contains(server)) await _release.Task.WaitAsync(cancellationToken);

                if (_missing.Contains(server)) return Text($@"[{{""sources"":[{{""file"":""/video/error""}}]}}]");
                if (!_playing.Contains(server)) return Status(HttpStatusCode.NotFound);

                return Text($@"[{{""sources"":[{{""file"":""https://cdn.example/{server}/master.m3u8""}}]}}]");
            }

            if (url.EndsWith("master.m3u8", StringComparison.Ordinal))
                return Text("#EXTM3U\n#EXTINF:6,\nsegment.ts\n");

            if (url.EndsWith("segment.ts", StringComparison.Ordinal))
            {
                var server = url["https://cdn.example/".Length..].Split('/')[0];

                return new HttpResponseMessage(HttpStatusCode.PartialContent)
                {
                    Content = new ByteArrayContent(Segment(_wrapped.Contains(server), _hollow.Contains(server)))
                };
            }

            return Status(HttpStatusCode.NotFound);
        }

        // Callers hold the lock: a slot is created by whichever of the test and the probe gets
        // there first.
        private TaskCompletionSource Slot(string server)
        {
            if (!_reached.TryGetValue(server, out var slot))
            {
                slot = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _reached[server] = slot;
            }

            return slot;
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

        private static HttpResponseMessage Text(string body)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8)
            };
        }

        private static HttpResponseMessage Status(HttpStatusCode code)
        {
            return new HttpResponseMessage(code) { Content = new StringContent("") };
        }
    }
}
