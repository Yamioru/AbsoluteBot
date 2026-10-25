using System.Net;
using AbsoluteBot.Services.NeuralNetworkServices;
using AbsoluteBot.Services.UtilityServices;
using AbsoluteBotTests.Infrastructure;

namespace AbsoluteBotTests.Services.Neural;

[Collection("BeginData")]
[Trait("Category", "Unit")]
[Trait("Area", "Services")]
public class NeuralRoutingTests(BeginDataFixture fixture)
{
    [Fact]
    public async Task Ask_UsesGeminiUrl_WhenProviderIsGemini()
    {
        _ = fixture;
        var geminiHandler = new MockHttpMessageHandler();
        geminiHandler.EnqueueResponse(HttpStatusCode.OK,
            """{"candidates":[{"content":{"parts":[{"text":"привет"}]}}]}""");
        var groqHandler = new MockHttpMessageHandler();

        var (service, models) = await CreateAskService(geminiHandler, groqHandler);
        await models.SetProviderAsync(NeuralProviders.Gemini);

        var answer = await service.AskAsync("hi", 50);

        Assert.Equal("привет", answer);
        Assert.Contains("generativelanguage.googleapis.com", geminiHandler.LastRequestUri, StringComparison.Ordinal);
        Assert.DoesNotContain("google_search", geminiHandler.LastBody);
        Assert.Empty(groqHandler.Requests);
    }

    [Fact]
    public async Task AskWithGoogleSearch_AddsGeminiSearchTool()
    {
        _ = fixture;
        var geminiHandler = new MockHttpMessageHandler();
        geminiHandler.EnqueueResponse(HttpStatusCode.OK,
            """{"candidates":[{"content":{"parts":[{"text":"нашёл"}]}}]}""");
        var groqHandler = new MockHttpMessageHandler();

        var (service, models) = await CreateAskService(geminiHandler, groqHandler);
        await models.SetProviderAsync(NeuralProviders.Gemini);

        var answer = await service.AskWithSearchAsync("погода", 50);

        Assert.Equal("нашёл", answer);
        Assert.Contains("google_search", geminiHandler.LastBody);
    }

    [Fact]
    public async Task Ask_UsesGroqUrl_WhenProviderIsGroq()
    {
        _ = fixture;
        var geminiHandler = new MockHttpMessageHandler();
        var groqHandler = new MockHttpMessageHandler();
        groqHandler.EnqueueResponse(HttpStatusCode.OK,
            """{"choices":[{"message":{"role":"assistant","content":"ok"}}]}""");

        var (service, models) = await CreateAskService(geminiHandler, groqHandler);
        await models.SetProviderAsync(NeuralProviders.Groq);

        var answer = await service.AskAsync("hi", 50);

        Assert.Equal("ok", answer);
        Assert.Equal(GroqChatService.ChatCompletionsUrl, groqHandler.LastRequestUri);
        Assert.StartsWith("Bearer ", groqHandler.LastAuthorization);
        Assert.Empty(geminiHandler.Requests);

        await models.SetProviderAsync(NeuralProviders.Gemini);
    }

    [Fact]
    public async Task AskEntity_UsesAskModel_OnGemini()
    {
        _ = fixture;
        var geminiHandler = new MockHttpMessageHandler();
        geminiHandler.EnqueueResponse(HttpStatusCode.OK,
            """{"candidates":[{"content":{"parts":[{"text":"ok"}]}}]}""");
        var groqHandler = new MockHttpMessageHandler();

        var (service, models) = await CreateAskService(geminiHandler, groqHandler);
        await models.SetProviderAsync(NeuralProviders.Gemini);

        var answer = await service.AskEntityAsync(NeuralEntities.Ask, "hi", 50, "будь краток", 0.2);

        Assert.Equal("ok", answer);
        Assert.Contains("gemini-3.1-flash-lite", geminiHandler.LastRequestUri, StringComparison.Ordinal);
        Assert.Empty(groqHandler.Requests);
    }

    [Fact]
    public async Task ChatHistory_ToGroqMessages_MapsRolesAndSkipsImages()
    {
        _ = fixture;
        var history = new ChatHistory();
        await history.AddMessageAsync("user", "привет", "Telegram");
        await history.AddMessageAsync("model", "ответ", "Telegram", "AAAABASE64");

        var messages = history.ToGroqMessages(new Dictionary<string, string>());

        Assert.Equal(2, messages.Count);
        Assert.Equal("user", messages[0].Role);
        Assert.Equal("привет", messages[0].Content);
        Assert.Equal("assistant", messages[1].Role);
        Assert.Equal("ответ", messages[1].Content);
    }

    [Fact]
    public async Task ChatHistory_ToGroqMessages_KeepsInitialAndRecentOnly()
    {
        _ = fixture;
        var history = new ChatHistory();
        await history.AddMessageAsync("user", "init-user", "Telegram");
        await history.AddMessageAsync("model", "init-model", "Telegram");
        for (var i = 0; i < 40; i++)
            await history.AddMessageAsync("user", $"msg-{i}", "Telegram");

        var messages = history.ToGroqMessages(new Dictionary<string, string>());

        Assert.Equal(32, messages.Count);
        Assert.Equal("init-user", messages[0].Content);
        Assert.Equal("init-model", messages[1].Content);
        Assert.Equal("msg-10", messages[2].Content);
        Assert.Equal("msg-39", messages[^1].Content);
    }

    private static async Task<(INeuralAskService Service, NeuralModelConfigService Models)> CreateAskService(
        MockHttpMessageHandler geminiHandler, MockHttpMessageHandler groqHandler)
    {
        var config = new ConfigService();
        await config.InitializeAsync();
        await config.SetConfigValueAsync("GroqApiKey", "test-groq-key");

        var settings = new GeminiSettingsProvider(config, new HttpClient(geminiHandler));
        await settings.InitializeAsync();
        settings.ApiKeys = new List<string> {"test-gemini-key"};

        var models = new NeuralModelConfigService();
        await models.InitializeAsync();

        var groq = new GroqChatService(config, new HttpClient(groqHandler));
        var geminiAsk = new GeminiAskClient(settings);
        return (new NeuralAskService(models, geminiAsk, groq), models);
    }
}
