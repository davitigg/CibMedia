using CibMedia.Core.Infrastructure.Updates;
using CibMedia.Core.Updates;
using Xunit;

namespace CibMedia.Core.Tests.Updates;

public sealed class AppManifestTests
{
    private const string Published = """
        {
          "versionCode": 4,
          "versionName": "2.1",
          "apkUrl": "https://github.com/o/r/releases/download/v2.1/cibmedia.apk",
          "sha256": "abc123",
          "sizeBytes": 28610000,
          "notes": "What changed",
          "playback": { "xpass": { "enabled": true } }
        }
        """;

    [Fact]
    public void A_published_manifest_reads_every_field()
    {
        var manifest = AppUpdatesClient.Parse(Published);

        Assert.NotNull(manifest);
        Assert.Equal(4, manifest.VersionCode);
        Assert.Equal("2.1", manifest.VersionName);
        Assert.Equal("https://github.com/o/r/releases/download/v2.1/cibmedia.apk", manifest.ApkUrl);
        Assert.Equal("abc123", manifest.Sha256);
        Assert.Equal(28610000, manifest.SizeBytes);
        Assert.Equal("What changed", manifest.Notes);
    }

    [Fact]
    public void The_playback_block_survives_as_the_text_it_arrived_as()
    {
        var manifest = AppUpdatesClient.Parse(Published);

        Assert.NotNull(manifest);
        Assert.Contains("\"xpass\"", manifest.PlaybackJson);
        Assert.DoesNotContain("versionCode", manifest.PlaybackJson);
    }

    [Fact]
    public void A_manifest_carrying_no_config_leaves_the_box_on_the_one_it_has()
    {
        var manifest = AppUpdatesClient.Parse("""{ "versionCode": 4 }""");

        Assert.NotNull(manifest);
        Assert.Null(manifest.PlaybackJson);
    }

    [Fact]
    public void A_field_this_build_predates_is_ignored_rather_than_fatal()
    {
        var manifest = AppUpdatesClient.Parse("""{ "versionCode": 4, "rolloutPercent": 50 }""");

        Assert.NotNull(manifest);
        Assert.Equal(4, manifest.VersionCode);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("[]")]
    [InlineData("""{ "versionName": "2.1" }""")]
    [InlineData("""{ "versionCode": "four" }""")]
    public void A_manifest_without_a_usable_version_is_no_manifest(string json)
    {
        Assert.Null(Read(json));
    }

    [Theory]
    [InlineData(3, 4, true)]
    [InlineData(4, 4, false)]
    [InlineData(5, 4, false)]
    public void Only_a_higher_version_is_offered(int installed, int published, bool offered)
    {
        Assert.Equal(offered, Release(published).ShouldOffer(installed, declinedVersionCode: 0));
    }

    [Fact]
    public void A_version_already_turned_down_is_not_offered_again()
    {
        Assert.False(Release(4).ShouldOffer(installedVersionCode: 3, declinedVersionCode: 4));
    }

    [Fact]
    public void Turning_one_down_does_not_silence_the_next()
    {
        Assert.True(Release(5).ShouldOffer(installedVersionCode: 3, declinedVersionCode: 4));
    }

    [Theory]
    [InlineData("apkUrl")]
    [InlineData("sha256")]
    public void A_release_that_carries_only_config_offers_nothing(string missing)
    {
        var manifest = AppUpdatesClient.Parse($$"""
            { "versionCode": 4, "apkUrl": "https://h/a.apk", "sha256": "abc" }
            """.Replace($"\"{missing}\": ", "\"unused\": "));

        Assert.NotNull(manifest);
        Assert.False(manifest.ShouldOffer(installedVersionCode: 3, declinedVersionCode: 0));
    }

    [Theory]
    [InlineData("ABC123", true)]
    [InlineData("abc123", true)]
    [InlineData(" abc123 ", true)]
    [InlineData("abc124", false)]
    [InlineData("", false)]
    public void Only_the_published_checksum_reaches_the_installer(string downloaded, bool accepted)
    {
        Assert.Equal(accepted, Release(4).MatchesChecksum(downloaded));
    }

    private static AppManifest Release(int versionCode)
    {
        var manifest = AppUpdatesClient.Parse(
            $$"""
            { "versionCode": {{versionCode}}, "apkUrl": "https://h/a.apk", "sha256": "abc123" }
            """);

        Assert.NotNull(manifest);

        return manifest;
    }

    private static AppManifest? Read(string json)
    {
        try
        {
            return AppUpdatesClient.Parse(json);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
