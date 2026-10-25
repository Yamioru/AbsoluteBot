using System.Collections.Concurrent;
using AbsoluteBot.Chat.Commands.AdminCommands;
using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services.CommandManagementServices;
using AbsoluteBot.Services.UtilityServices;
using AbsoluteBotTests.Infrastructure;
using Moq;

namespace AbsoluteBotTests.Commands.Admin;

[Trait("Category", "Unit")]
[Trait("Area", "Commands")]
public class CensorAndStatusCommandTests
{
    [Fact]
    public void ShowCensor_CanExecute_AdminChannelOnly()
    {
        var cmd = new ShowCensorWordsCommand(new Mock<CensorWordsService>().Object);
        Assert.True(cmd.CanExecute(ChatContextFactory.CreateAdminChannelParsed(cmd.Name)));
        Assert.False(cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name, role: UserRole.Administrator)));
    }

    [Fact]
    public async Task ShowCensor_HappyPath()
    {
        var censor = new Mock<CensorWordsService>();
        censor.Setup(c => c.GetAllCensorWords()).Returns(new ConcurrentBag<string> { "bad" });
        var cmd = new ShowCensorWordsCommand(censor.Object);

        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateAdminChannelParsed(cmd.Name));

        Assert.Contains("bad", result);
    }

    [Fact]
    public void AddCensor_CanExecute_AdminChannelOnly()
    {
        var cmd = new AddCensorWord(new Mock<CensorWordsService>().Object);
        Assert.True(cmd.CanExecute(ChatContextFactory.CreateAdminChannelParsed(cmd.Name)));
        Assert.False(cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name)));
    }

    [Fact]
    public async Task AddCensor_MissingParameters_ReturnsUsage()
    {
        var cmd = new AddCensorWord(new Mock<CensorWordsService>().Object);
        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateAdminChannelParsed(cmd.Name, ""));
        Assert.Contains("Использование", result);
    }

    [Fact]
    public async Task AddCensor_HappyPath()
    {
        var censor = new Mock<CensorWordsService>();
        censor.Setup(c => c.AddCensorWordAsync("bad")).ReturnsAsync(true);
        var cmd = new AddCensorWord(censor.Object);

        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateAdminChannelParsed(cmd.Name, "bad"));

        Assert.Contains("добавлено", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RemoveCensor_CanExecute_AdminChannelOnly()
    {
        var cmd = new RemoveCensorWordCommand(new Mock<CensorWordsService>().Object);
        Assert.True(cmd.CanExecute(ChatContextFactory.CreateAdminChannelParsed(cmd.Name)));
        Assert.False(cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name)));
    }

    [Fact]
    public async Task RemoveCensor_MissingParameters_ReturnsUsage()
    {
        var cmd = new RemoveCensorWordCommand(new Mock<CensorWordsService>().Object);
        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateAdminChannelParsed(cmd.Name, ""));
        Assert.Contains("Использование", result);
    }

    [Fact]
    public async Task RemoveCensor_HappyPath()
    {
        var censor = new Mock<CensorWordsService>();
        censor.Setup(c => c.RemoveCensorWordAsync("bad")).ReturnsAsync(true);
        var cmd = new RemoveCensorWordCommand(censor.Object);

        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateAdminChannelParsed(cmd.Name, "bad"));

        Assert.Contains("удалено", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShowStatuses_CanExecute_AdminChannelOnly()
    {
        var cmd = new ShowCommandStatusesCommand(new Mock<CommandStatusService>().Object);
        Assert.True(cmd.CanExecute(ChatContextFactory.CreateAdminChannelParsed(cmd.Name)));
        Assert.False(cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name)));
    }

    [Fact]
    public async Task ShowStatuses_HappyPath_Empty()
    {
        var statuses = new Mock<CommandStatusService>();
        statuses.Setup(s => s.GetAllCommandStatuses()).Returns(new ConcurrentDictionary<(string, string), bool>());
        var cmd = new ShowCommandStatusesCommand(statuses.Object);

        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateAdminChannelParsed(cmd.Name));

        Assert.Contains("Нет зарегистрированных", result);
    }

    [Fact]
    public void ToggleStatus_CanExecute_AdminChannelOnly()
    {
        var cmd = new ToggleCommandStatusCommand(new Mock<CommandStatusService>().Object);
        Assert.True(cmd.CanExecute(ChatContextFactory.CreateAdminChannelParsed(cmd.Name)));
        Assert.False(cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name)));
    }

    [Fact]
    public async Task ToggleStatus_MissingParameters_ReturnsUsage()
    {
        var cmd = new ToggleCommandStatusCommand(new Mock<CommandStatusService>().Object);
        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateAdminChannelParsed(cmd.Name, ""));
        Assert.Contains("Использование", result);
    }

    [Fact]
    public async Task ToggleStatus_IncompleteParameters_ReturnsUsage()
    {
        var cmd = new ToggleCommandStatusCommand(new Mock<CommandStatusService>().Object);
        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateAdminChannelParsed(cmd.Name, "!факт"));
        Assert.Contains("Использование", result);
    }

    [Fact]
    public async Task ToggleStatus_HappyPath()
    {
        var statuses = new Mock<CommandStatusService>();
        statuses.Setup(s => s.IsCommandEnabled("!факт", "Telegram")).ReturnsAsync(true);
        statuses.Setup(s => s.SetCommandStatusAsync("!факт", "Telegram", false)).ReturnsAsync(true);
        var cmd = new ToggleCommandStatusCommand(statuses.Object);

        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateAdminChannelParsed(cmd.Name, "!факт Telegram"));

        Assert.Contains("отключена", result);
    }
}
