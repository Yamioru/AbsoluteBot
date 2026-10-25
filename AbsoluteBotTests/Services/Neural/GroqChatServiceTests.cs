using System.Net;
using AbsoluteBot.Services.NeuralNetworkServices;
using AbsoluteBot.Services.UtilityServices;
using AbsoluteBotTests.Infrastructure;
using Moq;

namespace AbsoluteBotTests.Services.Neural;

[Trait("Category", "Unit")]
[Trait("Area", "Services")]
public class GroqChatServiceTests
{
    [Fact]
    public async Task CompleteAsync_RetriesWithoutMaxTokens_WhenModelRejectsLimit()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.BadRequest,
            """{"error":{"message":"`max_tokens` must be less than or equal to `512`"}}""");
        handler.EnqueueResponse(HttpStatusCode.OK,
            """{"choices":[{"message":{"role":"assistant","content":"ok"}}]}""");

        var config = new Mock<ConfigService>();
        config.Setup(c => c.GetConfigValueAsync<string>("GroqApiKey")).ReturnsAsync("key");

        var groq = new GroqChatService(config.Object, new HttpClient(handler));
        var answer = await groq.CompleteAsync(
            [new GroqChatMessage("user", "hi")],
            "meta-llama/llama-prompt-guard-2-86m",
            maxTokens: 2000);

        Assert.Equal("ok", answer);
        Assert.Equal(2, handler.Requests.Count);
        Assert.DoesNotContain("max_tokens", handler.LastBody);
    }
}
