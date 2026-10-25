using System.Net;
using AbsoluteBot.Services.MediaServices;
using AbsoluteBotTests.Infrastructure;

namespace AbsoluteBotTests.Services.Media;

[Trait("Category", "Unit")]
[Trait("Area", "Services")]
public class CatImageServiceTests
{
    [Fact]
    public async Task GetCatImageAsync_BuildsUrlFromId()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, """{"_id":"abc123","tags":["cute"]}""");
        var service = new CatImageService(new HttpClient(handler));

        var result = await service.GetCatImageAsync();

        Assert.Equal("https://cataas.com/cat/abc123", result);
    }

    [Fact]
    public async Task GetCatImageAsync_ReturnsDefault_OnFailure()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.InternalServerError, "err");
        var service = new CatImageService(new HttpClient(handler));

        var result = await service.GetCatImageAsync();

        Assert.Equal("https://i.ytimg.com/vi/KoVjqdETurw/maxresdefault.jpg", result);
    }

    [Fact]
    public async Task GetCatImageAsync_ReturnsDefault_OnException()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueException(new HttpRequestException("down"));
        var service = new CatImageService(new HttpClient(handler));

        var result = await service.GetCatImageAsync();

        Assert.Equal("https://i.ytimg.com/vi/KoVjqdETurw/maxresdefault.jpg", result);
    }
}
