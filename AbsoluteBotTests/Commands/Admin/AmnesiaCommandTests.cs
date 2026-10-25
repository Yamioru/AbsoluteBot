using AbsoluteBot.Chat.Commands.AdminCommands;
using AbsoluteBot.Models;
using AbsoluteBot.Services.NeuralNetworkServices;
using AbsoluteBotTests.Infrastructure;
using Moq;

namespace AbsoluteBotTests.Commands.Admin;

[Trait("Category", "Unit")]
[Trait("Area", "Commands")]
public class AmnesiaCommandTests
{
    [Theory]
    [InlineData(UserRole.Administrator, true)]
    [InlineData(UserRole.Bot, true)]
    [InlineData(UserRole.Moderator, false)]
    [InlineData(UserRole.Default, false)]
    public void CanExecute_ByRole(UserRole role, bool expected)
    {
        var cmd = new AmnesiaCommand(CommandTestMocks.CreateChatGeminiMock().Object);
        Assert.Equal(expected, cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name, role: role)));
    }

    [Fact]
    public async Task ExecuteAsync_ClearsHistory()
    {
        var gemini = CommandTestMocks.CreateChatGeminiMock();
        gemini.Setup(g => g.ClearPlatformChatHistoryAsync(It.IsAny<string>())).ReturnsAsync(true);
        var cmd = new AmnesiaCommand(gemini.Object);

        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name, role: UserRole.Administrator));

        Assert.Contains("успешно", result);
        gemini.Verify(g => g.ClearPlatformChatHistoryAsync("WebText"), Times.Once);
    }
}
