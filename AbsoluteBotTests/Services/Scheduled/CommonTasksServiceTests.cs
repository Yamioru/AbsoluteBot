using AbsoluteBot.Services.NeuralNetworkServices;
using AbsoluteBot.Services.ScheduledTasks;
using AbsoluteBot.Services.UtilityServices;
using Moq;

namespace AbsoluteBotTests.Services.Scheduled;

[Trait("Category", "Unit")]
[Trait("Area", "Services")]
public class CommonTasksServiceTests
{
    private static Mock<INeuralAskService> CreateGeminiMock() => new();

    [Fact]
    public async Task ExecuteDailyCommonTask_WritesNews_WhenGeminiSucceeds()
    {
        var gemini = CreateGeminiMock();
        gemini.Setup(g => g.AskAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<double>()))
            .ReturnsAsync("News item 1. News item 2.");

        var config = new Mock<ConfigService>();
        config.Setup(c => c.SetConfigValueAsync("News", It.IsAny<object>())).ReturnsAsync(true);

        var service = new CommonTasksService(gemini.Object, config.Object);
        await service.ExecuteDailyCommonTask();

        config.Verify(c => c.SetConfigValueAsync("News", "News item 1. News item 2."), Times.Once);
    }

    [Fact]
    public async Task ExecuteDailyCommonTask_DoesNotWrite_WhenGeminiReturnsError()
    {
        var gemini = CreateGeminiMock();
        gemini.Setup(g => g.AskAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<double>()))
            .ReturnsAsync("Ошибка");

        var config = new Mock<ConfigService>();
        var service = new CommonTasksService(gemini.Object, config.Object);
        await service.ExecuteDailyCommonTask();

        config.Verify(c => c.SetConfigValueAsync(It.IsAny<string>(), It.IsAny<object>()), Times.Never);
    }
}
