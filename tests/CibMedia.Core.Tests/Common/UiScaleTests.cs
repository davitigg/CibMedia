using CibMedia.Core.Common;
using Xunit;

namespace CibMedia.Core.Tests.Common;

public sealed class UiScaleTests
{
    [Fact]
    public void Leaves_a_panel_that_already_measures_the_design_width_alone()
    {
        Assert.Equal(320, UiScale.DensityDpiFor(960, 320, UiScaleStep.Normal));
    }

    // 1920px at density 240 is 1280dp, so a 112.5dp card covers a quarter less of the panel
    // than it was drawn to.
    [Fact]
    public void Corrects_a_panel_that_reports_a_width_the_layout_was_not_drawn_for()
    {
        var density = UiScale.DensityDpiFor(1280, 240, UiScaleStep.Normal);

        Assert.Equal(320, density);
        Assert.Equal(960, UiScale.Rescale(1280, 240, density));
    }

    [Fact]
    public void Corrects_a_panel_reporting_far_more_width_than_the_design()
    {
        var density = UiScale.DensityDpiFor(1920, 320, UiScaleStep.Normal);

        Assert.Equal(640, density);
        Assert.Equal(960, UiScale.Rescale(1920, 320, density));
    }

    [Theory]
    [InlineData(UiScaleStep.Smaller, 1280)]
    [InlineData(UiScaleStep.Small, 1097)]
    [InlineData(UiScaleStep.Normal, 960)]
    [InlineData(UiScaleStep.Large, 853)]
    [InlineData(UiScaleStep.Larger, 768)]
    public void Applies_the_users_step_on_top_of_the_correction(UiScaleStep step, int expectedWidthDp)
    {
        var density = UiScale.DensityDpiFor(1280, 240, step);

        Assert.Equal(expectedWidthDp, UiScale.Rescale(1280, 240, density));
    }

    [Fact]
    public void Steps_wrap_round_to_the_smallest()
    {
        Assert.Equal(UiScaleStep.Small, UiScale.Next(UiScaleStep.Smaller));
        Assert.Equal(UiScaleStep.Larger, UiScale.Next(UiScaleStep.Large));
        Assert.Equal(UiScaleStep.Smaller, UiScale.Next(UiScaleStep.Larger));
    }

    [Theory]
    [InlineData(0, 320)]
    [InlineData(-1, 320)]
    public void Keeps_the_reported_density_when_the_width_cannot_be_true(int widthDp, int densityDpi)
    {
        Assert.Equal(densityDpi, UiScale.DensityDpiFor(widthDp, densityDpi, UiScaleStep.Larger));
    }
}
