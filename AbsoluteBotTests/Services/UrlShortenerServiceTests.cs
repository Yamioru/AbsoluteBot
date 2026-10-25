using System.Net;
using AbsoluteBot.Services.UtilityServices;
using AbsoluteBotTests.Infrastructure;

namespace AbsoluteBotTests.Services;

[Trait("Category", "Unit")]
[Trait("Area", "Services")]
public class UrlShortenerServiceTests
{
    [Fact]
    public async Task ShrinkUrlAsync_ReturnsShortUrl()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, "https://tinyurl.com/abc", "text/plain");
        var service = new UrlShortenerService(new HttpClient(handler));

        var result = await service.ShrinkUrlAsync("https://example.com/long/path");

        Assert.Equal("https://tinyurl.com/abc", result);
        Assert.Contains("example.com", Uri.UnescapeDataString(handler.Requests[0].RequestUri!.Query));
    }

    [Fact]
    public async Task ShrinkUrlAsync_ReturnsNull_OnException()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueException(new HttpRequestException("fail"));
        var service = new UrlShortenerService(new HttpClient(handler));

        var result = await service.ShrinkUrlAsync("https://example.com");

        Assert.Null(result);
    }
}
