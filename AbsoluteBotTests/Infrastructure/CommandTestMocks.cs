using AbsoluteBot.Services;
using AbsoluteBot.Services.NeuralNetworkServices;
using AbsoluteBot.Services.UtilityServices;
using Moq;

namespace AbsoluteBotTests.Infrastructure;

internal static class CommandTestMocks
{
    public static Mock<INeuralAskService> CreateAskGeminiMock() => new();

    public static Mock<INeuralAskService> CreateAskNeuralMock() => new();

    public static Mock<ChatGptService> CreateChatGptMock() => new(new ConfigService());

    public static Mock<TranslationService> CreateTranslationMock() =>
        new(new HttpClient(), new ConfigService());

    public static Mock<INeuralChatService> CreateChatGeminiMock() => new();

    public static Mock<INeuralChatService> CreateChatNeuralMock() => new();
}
