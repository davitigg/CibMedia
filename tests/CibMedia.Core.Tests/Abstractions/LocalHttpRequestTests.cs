using CibMedia.Core.Abstractions.LocalHttp;
using Xunit;

namespace CibMedia.Core.Tests.Abstractions;

public sealed class LocalHttpRequestTests
{
    [Fact]
    public void Reads_a_field_from_a_form_body()
    {
        Assert.Equal("abc", Post("key=abc").Form("key"));
    }

    [Fact]
    public void Reads_a_field_that_is_not_the_first()
    {
        Assert.Equal("abc", Post("other=1&key=abc&more=2").Form("key"));
    }

    [Fact]
    public void Undoes_the_encoding_a_browser_applied()
    {
        Assert.Equal("a b/c", Post("key=a+b%2Fc").Form("key"));
    }

    [Fact]
    public void Trims_what_the_field_was_padded_with()
    {
        Assert.Equal("abc", Post("key=++abc++").Form("key"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("key=")]
    [InlineData("key=+++")]
    [InlineData("other=abc")]
    [InlineData("keyring=abc")]
    public void Returns_null_for_a_field_that_carries_nothing(string body)
    {
        Assert.Null(Post(body).Form("key"));
    }

    [Fact]
    public void Knows_a_post_from_a_get()
    {
        Assert.True(Post("key=abc").IsPost);
        Assert.False(new LocalHttpRequest("GET", "/", string.Empty).IsPost);
    }

    private static LocalHttpRequest Post(string body)
    {
        return new LocalHttpRequest("POST", "/", body);
    }
}
