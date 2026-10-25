using System.Net;
using AbsoluteBot.Services;
using AbsoluteBot.Services.UtilityServices;
using AbsoluteBotTests.Infrastructure;
using Moq;

namespace AbsoluteBotTests.Services;

[Trait("Category", "Unit")]
[Trait("Area", "Services")]
public class TranslationServiceTests
{
    [Fact]
    public async Task TranslateTextAsync_ReturnsNull_WhenNotConfigured()
    {
        var config = new Mock<ConfigService>();
        config.Setup(c => c.GetConfigValueAsync<string>("DeepLApiKey")).ReturnsAsync((string?)null);
        var service = new TranslationService(new HttpClient(), config.Object);
        await service.InitializeAsync();

        var result = await service.TranslateTextAsync("hello", "RU");

        Assert.Null(result);
    }

    [Fact]
    public async Task TranslateTextAsync_ParsesDeepLResponse()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK,
            """{"translations":[{"text":"привет","detected_source_language":"EN"}]}""");
        var config = new Mock<ConfigService>();
        config.Setup(c => c.GetConfigValueAsync<string>("DeepLApiKey")).ReturnsAsync("secret");
        var service = new TranslationService(new HttpClient(handler), config.Object);
        await service.InitializeAsync();

        var result = await service.TranslateTextAsync("hello", "RU");

        Assert.Equal("привет", result);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
    }

    [Fact]
    public async Task InitializeAsync_DoesNotThrow_WhenAuthorizationAlreadySet()
    {
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Bearer already-set");
        var config = new Mock<ConfigService>();
        config.Setup(c => c.GetConfigValueAsync<string>("DeepLApiKey")).ReturnsAsync("secret");
        var service = new TranslationService(httpClient, config.Object);

        await service.InitializeAsync();
        await service.InitializeAsync();
    }
}
