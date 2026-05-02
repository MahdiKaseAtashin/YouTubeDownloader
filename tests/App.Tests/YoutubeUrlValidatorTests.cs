using App.Application.Services;
using Xunit;

namespace App.Tests;

public sealed class YoutubeUrlValidatorTests
{
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", true)]
    [InlineData("https://youtu.be/dQw4w9WgXcQ", true)]
    [InlineData("https://m.youtube.com/watch?v=dQw4w9WgXcQ", true)]
    [InlineData("https://youtube.com/shorts/1oS5GQtV9z8?si=KO4NIsL6spC2F7Wm", true)]
    [InlineData("https://www.youtube.com/shorts/1oS5GQtV9z8", true)]
    [InlineData("not a url", false)]
    [InlineData("", false)]
    public void IsValid_detects_supported_hosts(string url, bool expected) =>
        Assert.Equal(expected, YoutubeUrlValidator.IsValid(url));

    [Fact]
    public void TryExtractVideoId_watch_url()
    {
        var id = YoutubeUrlValidator.TryExtractVideoId("https://www.youtube.com/watch?v=dQw4w9WgXcQ&feature=share");
        Assert.Equal("dQw4w9WgXcQ", id);
    }

    [Fact]
    public void TryExtractVideoId_shorts_url_with_query()
    {
        var id = YoutubeUrlValidator.TryExtractVideoId("https://youtube.com/shorts/1oS5GQtV9z8?si=KO4NIsL6spC2F7Wm");
        Assert.Equal("1oS5GQtV9z8", id);
    }
}
