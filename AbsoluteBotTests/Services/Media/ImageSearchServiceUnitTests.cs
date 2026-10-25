using System.Net;
using AbsoluteBot.Services.MediaServices;
using AbsoluteBotTests.Infrastructure;

namespace AbsoluteBotTests.Services.Media;

[Trait("Category", "Unit")]
[Trait("Area", "Services")]
public class ImageSearchServiceUnitTests
{
    [Fact]
    public async Task SearchImageAsync_ExtractsUrlFromHtml()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK,
            """img_url=https%3A%2F%2Fcdn.example.com%2Fcat.jpg&other=1""",
            "text/html");
        var service = new ImageSearchService(new HttpClient(handler));

        var result = await service.SearchImageAsync("cat");

        Assert.Equal("https://cdn.example.com/cat.jpg", result);
    }

    [Fact]
    public async Task SearchImageAsync_ReturnsPlaceholder_OnHttpError()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueException(new HttpRequestException("fail"));
        var service = new ImageSearchService(new HttpClient(handler));

        var result = await service.SearchImageAsync("x");

        Assert.Equal("https://i.ytimg.com/vi/KoVjqdETurw/maxresdefault.jpg", result);
    }
}
