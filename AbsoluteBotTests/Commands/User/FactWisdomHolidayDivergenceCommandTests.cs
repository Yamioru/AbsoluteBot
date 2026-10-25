using AbsoluteBot.Chat.Commands.UserCommands;
using AbsoluteBot.Models;
using AbsoluteBot.Services;
using AbsoluteBotTests.Infrastructure;
using Moq;

namespace AbsoluteBotTests.Commands.User;

[Trait("Category", "Unit")]
[Trait("Area", "Commands")]
public class FactWisdomHolidayDivergenceCommandTests
{
    [Theory]
    [InlineData(UserRole.Default, true)]
    [InlineData(UserRole.Ignored, false)]
    public void Fact_CanExecute_ByRole(UserRole role, bool expected)
    {
        var fact = new Mock<FactService>(new HttpClient());
        var gemini = CommandTestMocks.CreateAskGeminiMock();
        var cmd = new FactCommand(fact.Object, gemini.Object);
        Assert.Equal(expected, cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name, role: role)));
    }

    [Fact]
    public async Task Fact_ExecuteAsync_HappyPath()
    {
        var fact = new Mock<FactService>(new HttpClient());
        fact.Setup(f => f.GetFactAsync()).ReturnsAsync("Интересный факт");
        var gemini = CommandTestMocks.CreateAskGeminiMock();
        var cmd = new FactCommand(fact.Object, gemini.Object);

        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name));

        Assert.Equal("Интересный факт", result);
    }

    [Fact]
    public void Wisdom_CanExecute_OfficialChannel()
    {
        var cmd = new WisdomCommand(new Mock<WisdomService>().Object);
        Assert.True(cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name)));
        var general = new AbsoluteBot.Chat.Context.ParsedCommand(cmd.Name, "",
            ChatContextFactory.CreateTelegramContext(AbsoluteBot.Chat.Context.ChannelType.General), UserRole.Default);
        Assert.False(cmd.CanExecute(general));
    }

    [Fact]
    public async Task Wisdom_ExecuteAsync_HappyPath()
    {
        var wisdom = new Mock<WisdomService>();
        wisdom.Setup(w => w.GetRandomWisdomAsync()).ReturnsAsync("Будь собой");
        var cmd = new WisdomCommand(wisdom.Object);

        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name));

        Assert.Equal("Будь собой", result);
    }

    [Theory]
    [InlineData(UserRole.Default, true)]
    [InlineData(UserRole.Ignored, false)]
    public void Holiday_CanExecute_ByRole(UserRole role, bool expected)
    {
        var cmd = new HolidayCommand(new Mock<HolidaysService>().Object);
        Assert.Equal(expected, cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name, role: role)));
    }

    [Fact]
    public async Task Holiday_ExecuteAsync_HappyPath()
    {
        var holidays = new Mock<HolidaysService>();
        holidays.Setup(h => h.GetHoliday(It.IsAny<string>())).Returns("День теста");
        var cmd = new HolidayCommand(holidays.Object);

        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name));

        Assert.Equal("День теста", result);
    }

    [Fact]
    public void Divergence_CanExecute_NotStreaming()
    {
        var cmd = new DivergenceCommand(new Mock<DivergenceService>().Object);
        Assert.True(cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name)));
        Assert.False(cmd.CanExecute(ChatContextFactory.CreateStreamingParsed(cmd.Name)));
        Assert.False(cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name, role: UserRole.Bot)));
    }

    [Fact]
    public async Task Divergence_ExecuteAsync_HappyPath()
    {
        var divergence = new Mock<DivergenceService>();
        divergence.Setup(d => d.GetCurrentDivergence()).Returns("0.500000");
        var cmd = new DivergenceCommand(divergence.Object);

        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name));

        Assert.Contains("0.500000", result);
    }
}
