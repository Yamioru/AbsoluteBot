using AbsoluteBot.Chat.Commands.AdminCommands;
using AbsoluteBot.Models;
using AbsoluteBot.Services;
using AbsoluteBotTests.Infrastructure;
using Moq;

namespace AbsoluteBotTests.Commands.Admin;

[Trait("Category", "Unit")]
[Trait("Area", "Commands")]
public class AddWisdomCommandTests
{
    [Theory]
    [InlineData(UserRole.Administrator, true)]
    [InlineData(UserRole.Moderator, true)]
    [InlineData(UserRole.Default, false)]
    [InlineData(UserRole.Ignored, false)]
    public void CanExecute_ByRole(UserRole role, bool expected)
    {
        var cmd = new AddWisdomCommand(new Mock<WisdomService>().Object);
        var parsed = ChatContextFactory.CreateParsed(cmd.Name, role: role);
        Assert.Equal(expected, cmd.CanExecute(parsed));
    }

    [Fact]
    public async Task ExecuteAsync_MissingParameters_ReturnsUsage()
    {
        var cmd = new AddWisdomCommand(new Mock<WisdomService>().Object);
        var parsed = ChatContextFactory.CreateParsed(cmd.Name, "", role: UserRole.Administrator);

        var result = await cmd.ExecuteAsync(parsed);

        Assert.Contains("Использование", result);
    }

    [Fact]
    public async Task ExecuteAsync_HappyPath_AddsWisdom()
    {
        var wisdom = new Mock<WisdomService>();
        wisdom.Setup(w => w.AddWisdomAsync("мудрость")).ReturnsAsync(true);
        var cmd = new AddWisdomCommand(wisdom.Object);
        var parsed = ChatContextFactory.CreateParsed(cmd.Name, "мудрость", role: UserRole.Moderator);

        var result = await cmd.ExecuteAsync(parsed);

        Assert.Contains("добавлена", result);
        wisdom.Verify(w => w.AddWisdomAsync("мудрость"), Times.Once);
    }
}
