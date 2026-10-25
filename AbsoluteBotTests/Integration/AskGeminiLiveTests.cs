using AbsoluteBot.Services.NeuralNetworkServices;
using AbsoluteBot.Services.UtilityServices;
using AbsoluteBotTests.Infrastructure;

namespace AbsoluteBotTests.Integration;

[Collection("BeginData")]
[Trait("Category", "Integration")]
[Trait("RequiresVpn", "true")]
[Trait("LiveAi", "true")]
[Trait("Area", "Services")]
public class AskGeminiLiveTests(BeginDataFixture fixture)
{
    private static bool IsLiveAiEnabled() =>
        Environment.GetEnvironmentVariable("RUN_LIVE_AI_TESTS") == "1";

    [Fact]
    public async Task AskGeminiResponseAsync_ReturnsNonEmptyAnswer()
    {
        if (!IsLiveAiEnabled())
            return;
        _ = fixture;

        var config = new ConfigService();
        await config.InitializeAsync();
        var settings = new GeminiSettingsProvider(config, new HttpClient());
        await settings.InitializeAsync();
        var models = new NeuralModelConfigService();
        await models.InitializeAsync();
        var groq = new GroqChatService(config, new HttpClient());
        var geminiAsk = new GeminiAskClient(settings);
        var service = new NeuralAskService(models, geminiAsk, groq);

        var answer = await service.AskAsync("Ответь одним словом: да", 50);

        Assert.False(string.IsNullOrWhiteSpace(answer));
    }
}

[Collection("BeginData")]
[Trait("Category", "Integration")]
[Trait("RequiresVpn", "true")]
[Trait("LiveAi", "true")]
[Trait("Area", "Services")]
public class ChatGptLiveTests(BeginDataFixture fixture)
{
    private static bool IsLiveAiEnabled() =>
        Environment.GetEnvironmentVariable("RUN_LIVE_AI_TESTS") == "1";

    [Fact]
    public async Task AskChatGptAsync_ReturnsNonEmptyAnswer()
    {
        if (!IsLiveAiEnabled())
            return;
        _ = fixture;

        var config = new ConfigService();
        await config.InitializeAsync();
        var service = new ChatGptService(config);
        await service.InitializeAsync();

        var answer = await service.AskChatGptAsync("Ответь одним словом: да", 50);

        Assert.False(string.IsNullOrWhiteSpace(answer));
    }
}
