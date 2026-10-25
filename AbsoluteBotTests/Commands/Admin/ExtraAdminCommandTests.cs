using AbsoluteBot.Chat.Commands.AdminCommands;
using AbsoluteBot.Models;
using AbsoluteBot.Services.CommandManagementServices;
using AbsoluteBotTests.Infrastructure;
using Moq;

namespace AbsoluteBotTests.Commands.Admin;

[Trait("Category", "Unit")]
[Trait("Area", "Commands")]
public class ExtraAdminCommandTests
{
    [Theory]
    [InlineData(UserRole.Administrator, true)]
    [InlineData(UserRole.Moderator, true)]
    [InlineData(UserRole.Default, false)]
    public void AddExtra_CanExecute_ByRole(UserRole role, bool expected)
    {
        var cmd = new AddExtraCommand(new Mock<ExtraCommandsService>().Object);
        Assert.Equal(expected, cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name, role: role)));
    }

    [Fact]
    public async Task AddExtra_MissingParameters_ReturnsUsage()
    {
        var cmd = new AddExtraCommand(new Mock<ExtraCommandsService>().Object);
        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name, "", role: UserRole.Administrator));
        Assert.Contains("Использование", result);
    }

    [Fact]
    public async Task AddExtra_HappyPath()
    {
        var extras = new Mock<ExtraCommandsService>();
        extras.Setup(e => e.AddOrUpdateCommandAsync("!hi", "hello")).ReturnsAsync(true);
        var cmd = new AddExtraCommand(extras.Object);

        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name, "!hi hello", role: UserRole.Moderator));

        Assert.Contains("добавлена", result);
    }

    [Theory]
    [InlineData(UserRole.Administrator, true)]
    [InlineData(UserRole.Moderator, true)]
    [InlineData(UserRole.Default, false)]
    public void RemoveExtra_CanExecute_ByRole(UserRole role, bool expected)
    {
        var cmd = new RemoveExtraCommand(new Mock<ExtraCommandsService>().Object);
        Assert.Equal(expected, cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name, role: role)));
    }

    [Fact]
    public async Task RemoveExtra_MissingParameters_ReturnsUsage()
    {
        var cmd = new RemoveExtraCommand(new Mock<ExtraCommandsService>().Object);
        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name, "", role: UserRole.Administrator));
        Assert.Contains("Использование", result);
    }

    [Fact]
    public async Task RemoveExtra_HappyPath()
    {
        var extras = new Mock<ExtraCommandsService>();
        extras.Setup(e => e.RemoveCommandAsync("!hi")).ReturnsAsync(true);
        var cmd = new RemoveExtraCommand(extras.Object);

        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name, "!hi", role: UserRole.Administrator));

        Assert.Contains("удалена", result);
    }
}
