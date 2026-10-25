using System.Net;
using AbsoluteBot.Services.MediaServices;
using AbsoluteBotTests.Infrastructure;

namespace AbsoluteBotTests.Services.Media;

[Trait("Category", "Unit")]
[Trait("Area", "Services")]
public class VideoSearchServiceUnitTests
{
    [Fact]
    public async Task SearchVideoAsync_ExtractsVideoId()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK,
            """{"videoId":"dQw4w9WgXcQ"}""",
            "text/html");
        var service = new VideoSearchService(new HttpClient(handler));

        var result = await service.SearchVideoAsync("query");

        Assert.Equal("https://youtu.be/dQw4w9WgXcQ", result);
    }

    [Fact]
    public async Task SearchVideoAsync_ReturnsDefault_OnHttpError()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueException(new HttpRequestException("offline"));
        var service = new VideoSearchService(new HttpClient(handler));

        var result = await service.SearchVideoAsync("q");

        Assert.Equal("https://youtu.be/dQw4w9WgXcQ", result);
    }
}
