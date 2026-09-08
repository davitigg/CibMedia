using CibMedia.Core.Playback;
using Xunit;

namespace CibMedia.Core.Tests.Playback;

public sealed class LanguageOrderTests
{
    [Fact]
    public void The_settings_choice_leads_and_the_box_languages_follow()
    {
        Assert.Equal(["ka", "en", "ru"], LanguageOrder.Of("ka", ["en", "ru"]));
        Assert.Equal(["en", "ru"], LanguageOrder.Of(null, ["en", "ru"]));
    }

    [Fact]
    public void Codes_are_normalised_and_each_named_once()
    {
        Assert.Equal(["en", "ka"], LanguageOrder.Of(" EN ", ["en", "", "KA", "ka"]));
        Assert.Empty(LanguageOrder.Of(null, []));
    }
}
