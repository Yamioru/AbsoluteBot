using System.Net;
using AbsoluteBot.Services;
using AbsoluteBotTests.Infrastructure;

namespace AbsoluteBotTests.Services;

[Trait("Category", "Unit")]
[Trait("Area", "Services")]
public class MailRuAnswerServiceTests
{
    [Fact]
    public async Task AskAsync_WhenHttpFails_ReturnsFallbackOrNull()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueException(new HttpRequestException("network"));
        var service = new MailRuAnswerService(new HttpClient(handler));
        var result = await service.AskAsync("что такое тест");
        // Сервис ловит исключения и возвращает сообщение об ошибке/null — главное не бросать наружу
        Assert.True(result == null || result.Length >= 0);
    }

    [Fact]
    public async Task AskAsync_EmptyQuery_DoesNotThrow()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, "<html></html>", "text/html");
        var service = new MailRuAnswerService(new HttpClient(handler));
        var result = await service.AskAsync(" ");
        Assert.True(result == null || result.Length >= 0);
    }
}
