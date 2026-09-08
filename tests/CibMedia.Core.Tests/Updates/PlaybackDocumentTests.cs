using CibMedia.Core.Infrastructure.Playback;
using Xunit;

namespace CibMedia.Core.Tests.Updates;

public sealed class PlaybackDocumentTests
{
    private const string Complete = """
        {
          "videoDb": { "enabled": true, "hosts": ["videodb.test"] },
          "xpass": { "enabled": true, "playerHost": "play.test", "subtitleHost": "sub.test" },
          "liveball": {
            "enabled": true,
            "pageHosts": ["liveball.test"],
            "keyPages": ["https://liveball.test/news/ufc"]
          }
        }
        """;

    [Fact]
    public void A_document_the_next_launch_would_accept_is_kept()
    {
        Assert.True(PlaybackDocument.IsUsable(Complete));
    }

    [Fact]
    public void A_stack_turned_off_still_has_to_be_described()
    {
        Assert.True(PlaybackDocument.IsUsable(Complete.Replace("\"enabled\": true", "\"enabled\": false")));
    }

    [Theory]
    [InlineData("\"hosts\": [\"videodb.test\"]", "\"hosts\": []")]
    [InlineData("\"playerHost\": \"play.test\"", "\"playerHost\": \"\"")]
    [InlineData("\"pageHosts\": [\"liveball.test\"]", "\"pageHosts\": [\" \"]")]
    [InlineData("\"https://liveball.test/news/ufc\"", "\"http://liveball.test/news/ufc\"")]
    public void A_document_that_would_throw_at_launch_is_refused(string published, string broken)
    {
        Assert.False(PlaybackDocument.IsUsable(Complete.Replace(published, broken)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("not json at all")]
    [InlineData("{ \"videoDb\": { \"hosts\": \"one string, not a list\" } }")]
    public void A_document_that_is_not_one_is_refused(string json)
    {
        Assert.False(PlaybackDocument.IsUsable(json));
    }
}
