using CibMedia.Core.Infrastructure.Playback;
using CibMedia.Playback;
using CibMedia.Playback.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CibMedia.Core.Tests.Infrastructure;

public sealed class LocalPlaybackCacheTests
{
    [Fact]
    public void Clearing_drops_what_the_resolvers_are_holding()
    {
        var provider = Resolvers();
        var held = provider.GetRequiredService<IMemoryCache>();

        held.Set("playback:videodb:Movie:603", "a resolved playlist");
        held.Set("playback:liveball:key", "a recovered token key");

        new LocalPlaybackCache(provider.GetRequiredService<PlaybackCacheService>()).Clear();

        Assert.False(held.TryGetValue("playback:videodb:Movie:603", out _));
        Assert.False(held.TryGetValue("playback:liveball:key", out _));
    }

    private static ServiceProvider Resolvers()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddPlayback(new PlaybackOptions
        {
            VideoDb = new PlaybackOptions.VideoDbSettings { Enabled = true, Hosts = ["videodb.test"] },
            Xpass = new PlaybackOptions.XpassSettings { PlayerHost = "player.test", SubtitleHost = "subs.test" },
            Liveball = new PlaybackOptions.LiveballSettings
            {
                Enabled = true,
                PageHosts = ["liveball.test"],
                KeyPages = ["https://liveball.test/news"]
            }
        });

        return services.BuildServiceProvider();
    }
}
