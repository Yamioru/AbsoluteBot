using AbsoluteBot.Chat.Commands.AdminCommands;
using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services;
using AbsoluteBot.Services.UserManagementServices;
using AbsoluteBot.Services.UtilityServices;
using AbsoluteBotTests.Infrastructure;
using Moq;

namespace AbsoluteBotTests.Commands.Admin;

[Trait("Category", "Unit")]
[Trait("Area", "Commands")]
public class ConfigComplaintsBirthdayAdminCommandTests
{
    [Fact]
    public void SetConfig_CanExecute_AdminChannelAndAdministrator()
    {
        var cmd = new SetConfigValueCommand(new Mock<ConfigService>().Object);
        Assert.True(cmd.CanExecute(ChatContextFactory.CreateAdminChannelParsed(cmd.Name, role: UserRole.Administrator)));
        Assert.False(cmd.CanExecute(ChatContextFactory.CreateAdminChannelParsed(cmd.Name, role: UserRole.Moderator)));
        Assert.False(cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name, role: UserRole.Administrator)));
    }

    [Fact]
    public async Task SetConfig_MissingParameters_ReturnsUsage()
    {
        var cmd = new SetConfigValueCommand(new Mock<ConfigService>().Object);
        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateAdminChannelParsed(cmd.Name, ""));
        Assert.Contains("Использование", result);
    }

    [Fact]
    public async Task SetConfig_HappyPath()
    {
        var config = new Mock<ConfigService>();
        config.Setup(c => c.SetConfigValueAsync("Key", "Value")).ReturnsAsync(true);
        var cmd = new SetConfigValueCommand(config.Object);

        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateAdminChannelParsed(cmd.Name, "Key Value"));

        Assert.Contains("Key", result);
        Assert.Contains("Value", result);
    }

    [Fact]
    public void ShowConfig_CanExecute_AdminChannelAndAdministrator()
    {
        var cmd = new ShowAllConfigCommand(new Mock<ConfigService>().Object);
        Assert.True(cmd.CanExecute(ChatContextFactory.CreateAdminChannelParsed(cmd.Name)));
        Assert.False(cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name, role: UserRole.Administrator)));
    }

    [Fact]
    public async Task ShowConfig_HappyPath()
    {
        var config = new Mock<ConfigService>();
        config.Setup(c => c.GetAllConfigValues()).Returns(new Dictionary<string, string> { ["A"] = "1" });
        var cmd = new ShowAllConfigCommand(config.Object);

        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateAdminChannelParsed(cmd.Name));

        Assert.Contains("A: 1", result);
    }

    [Fact]
    public void ShowComplaints_CanExecute_AdminOrPremium()
    {
        var cmd = new ShowComplaintsCommand(new Mock<ComplaintService>().Object);
        Assert.True(cmd.CanExecute(ChatContextFactory.CreateAdminChannelParsed(cmd.Name)));

        var premium = new ParsedCommand(cmd.Name, "", ChatContextFactory.CreateTelegramContext(ChannelType.Premium), UserRole.Default);
        Assert.True(cmd.CanExecute(premium));
        Assert.False(cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name)));
    }

    [Fact]
    public async Task ShowComplaints_HappyPath_Empty()
    {
        var complaints = new Mock<ComplaintService>();
        complaints.Setup(c => c.GetLastComplaints(It.IsAny<int>())).Returns(new List<string>());
        var cmd = new ShowComplaintsCommand(complaints.Object);

        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateAdminChannelParsed(cmd.Name));

        Assert.Contains("жалоб нет", result);
    }

    [Fact]
    public void ListBirthdays_CanExecute_AdminChannelOnly()
    {
        var gemini = CommandTestMocks.CreateAskGeminiMock().Object;
        var cmd = new ListBirthdaysCommand(new Mock<BirthdayService>(gemini).Object);
        Assert.True(cmd.CanExecute(ChatContextFactory.CreateAdminChannelParsed(cmd.Name)));
        Assert.False(cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name)));
    }

    [Fact]
    public async Task ListBirthdays_HappyPath_Empty()
    {
        var gemini = CommandTestMocks.CreateAskGeminiMock().Object;
        var birthdays = new Mock<BirthdayService>(gemini);
        birthdays.Setup(b => b.GetAllBirthdays()).Returns(new List<AbsoluteBot.Models.UserBirthdayInfo>());
        var cmd = new ListBirthdaysCommand(birthdays.Object);

        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateAdminChannelParsed(cmd.Name));

        Assert.Contains("не найдены", result);
    }
}
