using AbsoluteBot.Chat.Commands.UserCommands;
using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services.UserManagementServices;
using AbsoluteBotTests.Infrastructure;
using Moq;

namespace AbsoluteBotTests.Commands.User;

[Trait("Category", "Unit")]
[Trait("Area", "Commands")]
public class BirthdayCommandTests
{
    private static Mock<BirthdayService> CreateBirthdayMock() =>
        new(CommandTestMocks.CreateAskGeminiMock().Object);

    [Fact]
    public void Birthday_CanExecute_OfficialChannel()
    {
        var cmd = new BirthdayCommand(CreateBirthdayMock().Object);
        Assert.True(cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name)));
        var general = new ParsedCommand(cmd.Name, "", ChatContextFactory.CreateTelegramContext(ChannelType.General), UserRole.Default);
        Assert.False(cmd.CanExecute(general));
    }

    [Fact]
    public async Task Birthday_HappyPath()
    {
        var birthdays = CreateBirthdayMock();
        birthdays.Setup(b => b.GetTimeUntilNextBirthdayForPlatform("WebText")).Returns("Через 5 дней");
        var cmd = new BirthdayCommand(birthdays.Object);

        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name, ""));

        Assert.Equal("Через 5 дней", result);
    }

    [Fact]
    public void AddBirthday_CanExecute()
    {
        var cmd = new AddBirthdayCommand(CreateBirthdayMock().Object);
        Assert.True(cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name)));
        Assert.False(cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name, role: UserRole.Ignored)));
    }

    [Fact]
    public async Task AddBirthday_MissingParameters_ReturnsUsage()
    {
        var cmd = new AddBirthdayCommand(CreateBirthdayMock().Object);
        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name, ""));
        Assert.Contains("Использование", result);
    }

    [Fact]
    public async Task AddBirthday_HappyPath()
    {
        var birthdays = CreateBirthdayMock();
        birthdays.Setup(b => b.AddOrUpdateUserBirthday(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string?>())).ReturnsAsync(true);
        var cmd = new AddBirthdayCommand(birthdays.Object);

        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name, "23 09"));

        Assert.Contains("успешно добавлен", result);
    }

    [Fact]
    public void RemoveBirthday_CanExecute()
    {
        var cmd = new RemoveBirthdayCommand(CreateBirthdayMock().Object);
        Assert.True(cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name)));
        Assert.False(cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name, role: UserRole.Ignored)));
    }

    [Fact]
    public async Task RemoveBirthday_HappyPath()
    {
        var birthdays = CreateBirthdayMock();
        birthdays.Setup(b => b.DisableBirthdayNotificationForPlatformAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        var cmd = new RemoveBirthdayCommand(birthdays.Object);

        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name));

        Assert.False(string.IsNullOrEmpty(result));
    }
}
