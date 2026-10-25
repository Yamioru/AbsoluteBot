using AbsoluteBot.Chat.Commands.AdminCommands;
using AbsoluteBot.Models;
using AbsoluteBotTests.Infrastructure;

namespace AbsoluteBotTests.Commands.Admin;

[Trait("Category", "Unit")]
[Trait("Area", "Commands")]
public class RemindCommandTests
{
    [Theory]
    [InlineData(UserRole.Administrator, true)]
    [InlineData(UserRole.Moderator, true)]
    [InlineData(UserRole.Default, false)]
    public void CanExecute_ByRole(UserRole role, bool expected)
    {
        var cmd = new RemindCommand();
        Assert.Equal(expected, cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name, role: role)));
    }

    [Fact]
    public async Task ExecuteAsync_MissingParameters_ReturnsUsage()
    {
        var cmd = new RemindCommand();
        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name, "", role: UserRole.Administrator));
        Assert.Contains("Использование", result);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidParameters_ReturnsUsage()
    {
        var cmd = new RemindCommand();
        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name, "abc text", role: UserRole.Moderator));
        Assert.Contains("Использование", result);
    }
}
