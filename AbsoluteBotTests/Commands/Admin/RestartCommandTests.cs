using AbsoluteBot.Chat.Commands.AdminCommands;
using AbsoluteBot.Models;
using AbsoluteBot.Services.UtilityServices;
using AbsoluteBotTests.Infrastructure;
using Moq;

namespace AbsoluteBotTests.Commands.Admin;

[Trait("Category", "Unit")]
[Trait("Area", "Commands")]
public class RestartCommandTests
{
    [Fact]
    public void CanExecute_Administrator_ReturnsTrue()
    {
        var cmd = new RestartCommand(Mock.Of<IProcessController>());
        var parsed = ChatContextFactory.CreateParsed(cmd.Name, role: UserRole.Administrator);
        Assert.True(cmd.CanExecute(parsed));
    }

    [Theory]
    [InlineData(UserRole.Default)]
    [InlineData(UserRole.Moderator)]
    [InlineData(UserRole.Ignored)]
    [InlineData(UserRole.Bot)]
    public void CanExecute_NonAdministrator_ReturnsFalse(UserRole role)
    {
        var cmd = new RestartCommand(Mock.Of<IProcessController>());
        var parsed = ChatContextFactory.CreateParsed(cmd.Name, role: role);
        Assert.False(cmd.CanExecute(parsed));
    }

    [Fact]
    public async Task ExecuteAsync_CallsRestart()
    {
        var process = new Mock<IProcessController>();
        var cmd = new RestartCommand(process.Object);
        var chat = new FakeChatService();
        var parsed = ChatContextFactory.CreateParsed(cmd.Name, chatService: chat, role: UserRole.Administrator);

        var result = await cmd.ExecuteAsync(parsed);

        Assert.Contains("Перезагрузка", result);
        await Task.Delay(100);
        process.Verify(p => p.Restart(), Times.AtLeastOnce);
    }
}
