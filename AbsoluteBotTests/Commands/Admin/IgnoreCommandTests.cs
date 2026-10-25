using AbsoluteBot.Chat.Commands.AdminCommands;
using AbsoluteBot.Models;
using AbsoluteBot.Services.UserManagementServices;
using AbsoluteBotTests.Infrastructure;
using Moq;

namespace AbsoluteBotTests.Commands.Admin;

[Trait("Category", "Unit")]
[Trait("Area", "Commands")]
public class IgnoreCommandTests
{
    [Theory]
    [InlineData(UserRole.Administrator, true)]
    [InlineData(UserRole.Moderator, true)]
    [InlineData(UserRole.Bot, true)]
    [InlineData(UserRole.Default, false)]
    public void CanExecute_ByRole(UserRole role, bool expected)
    {
        var cmd = new IgnoreCommand(new Mock<RoleService>().Object);
        Assert.Equal(expected, cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name, role: role)));
    }

    [Fact]
    public async Task ExecuteAsync_MissingParameters_ReturnsUsage()
    {
        var cmd = new IgnoreCommand(new Mock<RoleService>().Object);
        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name, "", role: UserRole.Administrator));
        Assert.Contains("Использование", result);
    }

    [Fact]
    public async Task ExecuteAsync_AddsToIgnoreList()
    {
        var roles = new Mock<RoleService>();
        roles.Setup(r => r.GetUserRoleAsync("bob")).ReturnsAsync(UserRole.Default);
        roles.Setup(r => r.SetUserRoleAsync("bob", UserRole.Ignored)).ReturnsAsync(true);
        var cmd = new IgnoreCommand(roles.Object);

        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name, "bob", role: UserRole.Administrator));

        Assert.Contains("игнорируемых", result);
        roles.Verify(r => r.SetUserRoleAsync("bob", UserRole.Ignored), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_RemovesFromIgnoreList()
    {
        var roles = new Mock<RoleService>();
        roles.Setup(r => r.GetUserRoleAsync("bob")).ReturnsAsync(UserRole.Ignored);
        roles.Setup(r => r.SetUserRoleAsync("bob", UserRole.Default)).ReturnsAsync(true);
        var cmd = new IgnoreCommand(roles.Object);

        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name, "bob", role: UserRole.Moderator));

        Assert.Contains("больше не игнорируется", result);
    }
}
