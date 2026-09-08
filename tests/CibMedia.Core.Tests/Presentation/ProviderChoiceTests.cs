using CibMedia.Core.Presentation.Playback;
using Xunit;

namespace CibMedia.Core.Tests.Presentation;

public sealed class ProviderChoiceTests
{
    [Fact]
    public void Prefers_the_wanted_provider_when_the_title_has_it()
    {
        string[] providers = ["VideoDb", "Xpass"];

        Assert.Equal("Xpass", ProviderChoice.Preferred(providers, "xpass"));
        Assert.Equal("VideoDb", ProviderChoice.Preferred(providers, "Nowhere"));
        Assert.Equal("VideoDb", ProviderChoice.Preferred(providers, null));
        Assert.Null(ProviderChoice.Preferred([], "Xpass"));
    }

    [Fact]
    public void Moves_to_the_next_and_wraps_to_the_first()
    {
        string[] providers = ["VideoDb", "Xpass"];

        Assert.Equal("Xpass", ProviderChoice.Next(providers, "videodb"));
        Assert.Equal("VideoDb", ProviderChoice.Next(providers, "Xpass"));
    }

    [Fact]
    public void Starts_from_the_first_when_nothing_or_something_else_is_current()
    {
        string[] providers = ["VideoDb", "Xpass"];

        Assert.Equal("VideoDb", ProviderChoice.Next(providers, null));
        Assert.Equal("VideoDb", ProviderChoice.Next(providers, "Nowhere"));
        Assert.Null(ProviderChoice.Next([], "VideoDb"));
    }
}
