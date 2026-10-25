using System.Net;
using AbsoluteBot.Services;
using AbsoluteBotTests.Infrastructure;

namespace AbsoluteBotTests.Services.Media;

[Trait("Category", "Unit")]
[Trait("Area", "Services")]
public class HowLongToBeatServiceUnitTests
{
    [Fact]
    public async Task GetGameDurationAsync_ReturnsZero_WhenAuthTokenMissing()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, """{"token":null}""");
        var service = new HowLongToBeatService(new HttpClient(handler));

        var result = await service.GetGameDurationAsync("Some Game");

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task GetGameDurationAsync_ParsesCompMainMinutes()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK,
            """{"token":"t","hpKey":"k","hpVal":"v"}""");
        handler.EnqueueResponse(HttpStatusCode.OK,
            """{"data":[{"comp_main":3600,"comp_plus":0,"comp_100":0}]}""");
        var service = new HowLongToBeatService(new HttpClient(handler));

        var result = await service.GetGameDurationAsync("Test Game");

        Assert.Equal(60, result);
    }
}
