using System.Net;
using AbsoluteBot.Services.MediaServices;
using AbsoluteBotTests.Infrastructure;

namespace AbsoluteBotTests.Services.Media;

[Trait("Category", "Unit")]
[Trait("Area", "Services")]
public class GifSearchServiceTests
{
    [Fact]
    public async Task SearchGifAsync_ReturnsPlaceholder_WhenNoGifInPage()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, "<html>no gifs here</html>", "text/html");
        var service = new GifSearchService(new HttpClient(handler));

        var result = await service.SearchGifAsync("cats");

        Assert.Equal("https://media.tenor.com/8WQ2kuLp2MUAAAAM/shlepa.gif", result);
    }

    [Fact]
    public async Task SearchGifAsync_ReturnsValidGif_WhenHeadSucceeds()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK,
            """https://cdn.example.com/funny.gif" """,
            "text/html");
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(Array.Empty<byte>())
            {
                Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/gif") }
            }
        });
        var service = new GifSearchService(new HttpClient(handler));

        var result = await service.SearchGifAsync("funny");

        Assert.Equal("https://cdn.example.com/funny.gif", result);
    }

    [Fact]
    public async Task SearchGifAsync_ReturnsPlaceholder_OnException()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueException(new HttpRequestException("fail"));
        var service = new GifSearchService(new HttpClient(handler));

        var result = await service.SearchGifAsync("x");

        Assert.Equal("https://media.tenor.com/8WQ2kuLp2MUAAAAM/shlepa.gif", result);
    }
}
