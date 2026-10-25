using AbsoluteBot.Chat.Commands.AdminCommands;
using AbsoluteBot.Models;
using AbsoluteBot.Services.CommandManagementServices;
using AbsoluteBot.Services.UtilityServices;
using AbsoluteBotTests.Infrastructure;
using Moq;

namespace AbsoluteBotTests.Commands.Admin;

[Trait("Category", "Unit")]
[Trait("Area", "Commands")]
public class CooldownAndTranslateCommandTests
{
    [Theory]
    [InlineData(UserRole.Administrator, true)]
    [InlineData(UserRole.Moderator, true)]
    [InlineData(UserRole.Default, false)]
    public void Cooldown_CanExecute_ByRole(UserRole role, bool expected)
    {
        var cmd = new CooldownCommand(new Mock<CooldownService>().Object);
        Assert.Equal(expected, cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name, role: role)));
    }

    [Fact]
    public async Task Cooldown_MissingParameters_ReturnsUsage()
    {
        var cmd = new CooldownCommand(new Mock<CooldownService>().Object);
        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name, "", role: UserRole.Administrator));
        Assert.Contains("Использование", result);
    }

    [Fact]
    public async Task Cooldown_HappyPath()
    {
        var cooldown = new Mock<CooldownService>();
        cooldown.Setup(c => c.SetCooldownAsync("WebText", "30")).ReturnsAsync(true);
        var cmd = new CooldownCommand(cooldown.Object);

        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name, "30", role: UserRole.Moderator));

        Assert.Contains("Перезарядка установлена", result);
    }

    [Theory]
    [InlineData(UserRole.Administrator, true)]
    [InlineData(UserRole.Moderator, true)]
    [InlineData(UserRole.Default, false)]
    public void AutoTranslate_CanExecute_ByRole(UserRole role, bool expected)
    {
        var translation = CommandTestMocks.CreateTranslationMock().Object;
        var cmd = new AutoTranslateCommand(new Mock<AutoTranslateService>(translation).Object);
        Assert.Equal(expected, cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name, role: role)));
    }

    [Fact]
    public async Task AutoTranslate_MissingParameters_ReturnsUsage()
    {
        var translation = CommandTestMocks.CreateTranslationMock().Object;
        var cmd = new AutoTranslateCommand(new Mock<AutoTranslateService>(translation).Object);
        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name, "", role: UserRole.Administrator));
        Assert.Contains("Использование", result);
    }

    [Fact]
    public async Task AutoTranslate_HappyPath()
    {
        var translation = CommandTestMocks.CreateTranslationMock().Object;
        var auto = new Mock<AutoTranslateService>(translation);
        auto.Setup(a => a.ToggleUserAutoTranslateAsync("bob")).ReturnsAsync(true);
        var cmd = new AutoTranslateCommand(auto.Object);

        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name, "bob", role: UserRole.Administrator));

        Assert.Contains("bob", result);
        auto.Verify(a => a.ToggleUserAutoTranslateAsync("bob"), Times.Once);
    }
}
