using AbsoluteBot.Chat.Commands.AdminCommands;
using AbsoluteBot.Models;
using AbsoluteBot.Services.UtilityServices;
using AbsoluteBotTests.Infrastructure;
using Moq;

namespace AbsoluteBotTests.Commands.Admin;

[Trait("Category", "Unit")]
[Trait("Area", "Commands")]
public class UpdateBotCommandTests
{
    [Fact]
    public void CanExecute_Administrator_ReturnsTrue()
    {
        var cmd = new UpdateBotCommand(Mock.Of<IBotUpdateService>());
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
        var cmd = new UpdateBotCommand(Mock.Of<IBotUpdateService>());
        var parsed = ChatContextFactory.CreateParsed(cmd.Name, role: role);
        Assert.False(cmd.CanExecute(parsed));
    }

    [Fact]
    public async Task ExecuteAsync_RequestsUpdate()
    {
        var update = new Mock<IBotUpdateService>();
        var cmd = new UpdateBotCommand(update.Object);
        var chat = new FakeChatService();
        var parsed = ChatContextFactory.CreateParsed(cmd.Name, chatService: chat, role: UserRole.Administrator);

        var result = await cmd.ExecuteAsync(parsed);

        Assert.Contains("Обновление запрошено", result);
        update.Verify(u => u.RequestUpdateAsync(), Times.Once);
    }
}
