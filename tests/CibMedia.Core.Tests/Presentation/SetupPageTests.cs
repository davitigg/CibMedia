using CibMedia.Core.Presentation.Setup;
using Xunit;

namespace CibMedia.Core.Tests.Presentation;

// The page is an embedded file looked up by a name built from the folder layout, which nothing
// checks at compile time.
public sealed class SetupPageTests
{
    [Fact]
    public void Is_shipped_inside_the_assembly()
    {
        Assert.Contains("<!doctype html>", SetupPage.Html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Posts_the_keys_back_to_the_path_it_was_served_from()
    {
        Assert.Contains("location.pathname", SetupPage.Html, StringComparison.Ordinal);
    }

    [Fact]
    public void Keeps_no_opinion_about_what_a_key_looks_like()
    {
        Assert.DoesNotContain("[0-9a-f]", SetupPage.Html, StringComparison.OrdinalIgnoreCase);
    }
}
