using AbsoluteBot.Chat.Commands.AdminCommands;
using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBotTests.Infrastructure;

namespace AbsoluteBotTests.Commands.Admin;

[Trait("Category", "Unit")]
[Trait("Area", "Commands")]
public class DeleteMessagesCommandTests
{
    [Theory]
    [InlineData(UserRole.Administrator, true)]
    [InlineData(UserRole.Moderator, false)]
    [InlineData(UserRole.Default, false)]
    public void CanExecute_ByRole(UserRole role, bool expected)
    {
        var cmd = new DeleteMessagesCommand();
        Assert.Equal(expected, cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name, role: role)));
    }

    [Fact]
    public async Task ExecuteAsync_MissingParameters_ReturnsUsage()
    {
        var cmd = new DeleteMessagesCommand();
        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name, "", role: UserRole.Administrator));
        Assert.Contains("Использование", result);
    }

    [Fact]
    public async Task ExecuteAsync_HappyPath_DeletesMessages()
    {
        var chat = new FakeDeletingChatService();
        var cmd = new DeleteMessagesCommand();
        var parsed = ChatContextFactory.CreateParsed(cmd.Name, "5", chat, UserRole.Administrator);

        var result = await cmd.ExecuteAsync(parsed);

        Assert.Contains("Удалено 5", result);
        Assert.Single(chat.DeletedBatches);
        Assert.Equal(5, chat.DeletedBatches[0].Count);
    }

    [Fact]
    public async Task ExecuteAsync_UnsupportedPlatform_ReturnsMessage()
    {
        var cmd = new DeleteMessagesCommand();
        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name, "3", role: UserRole.Administrator));
        Assert.Contains("не поддерживает", result);
    }
}
