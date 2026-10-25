using AbsoluteBot.Chat.Commands.UserCommands;
using AbsoluteBot.Models;
using AbsoluteBot.Services;
using AbsoluteBotTests.Infrastructure;
using Moq;

namespace AbsoluteBotTests.Commands.User;

[Trait("Category", "Unit")]
[Trait("Area", "Commands")]
public class TranslateComplaintHoroscopeAnimeCommandTests
{
    [Theory]
    [InlineData(UserRole.Default, true)]
    [InlineData(UserRole.Ignored, false)]
    public void Translate_CanExecute_ByRole(UserRole role, bool expected)
    {
        var cmd = new TranslateCommand(CommandTestMocks.CreateTranslationMock().Object);
        Assert.Equal(expected, cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name, role: role)));
    }

    [Fact]
    public async Task Translate_MissingParameters_ReturnsUsage()
    {
        var cmd = new TranslateCommand(CommandTestMocks.CreateTranslationMock().Object);
        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name, ""));
        Assert.Contains("Использование", result);
    }

    [Fact]
    public async Task Translate_HappyPath()
    {
        var translation = CommandTestMocks.CreateTranslationMock();
        translation.Setup(t => t.TranslateTextAsync("hello", "RU")).ReturnsAsync("привет");
        var cmd = new TranslateCommand(translation.Object);

        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name, "hello"));

        Assert.Equal("привет", result);
    }

    [Theory]
    [InlineData(UserRole.Default, true)]
    [InlineData(UserRole.Ignored, false)]
    public void Complaint_CanExecute_ByRole(UserRole role, bool expected)
    {
        var cmd = new ComplaintCommand(new Mock<ComplaintService>().Object);
        Assert.Equal(expected, cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name, role: role)));
    }

    [Fact]
    public async Task Complaint_MissingParameters_ReturnsUsage()
    {
        var cmd = new ComplaintCommand(new Mock<ComplaintService>().Object);
        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name, ""));
        Assert.Contains("Использование", result);
    }

    [Fact]
    public async Task Complaint_HappyPath()
    {
        var complaints = new Mock<ComplaintService>();
        complaints.Setup(c => c.AddComplaintAsync("сломалось")).ReturnsAsync(true);
        var cmd = new ComplaintCommand(complaints.Object);

        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name, "сломалось"));

        Assert.Contains("успешно", result);
    }

    [Theory]
    [InlineData(UserRole.Default, true)]
    [InlineData(UserRole.Ignored, false)]
    public void Horoscope_CanExecute_ByRole(UserRole role, bool expected)
    {
        var cmd = new HoroscopeCommand(CommandTestMocks.CreateAskGeminiMock().Object);
        Assert.Equal(expected, cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name, role: role)));
    }

    [Fact]
    public async Task Horoscope_HappyPath()
    {
        var gemini = CommandTestMocks.CreateAskGeminiMock();
        gemini.Setup(g => g.AskAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<double>()))
            .ReturnsAsync("Согласно гороскопу сегодня вам повезёт");
        var cmd = new HoroscopeCommand(gemini.Object);

        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name));

        Assert.Contains("гороскопу", result);
    }

    [Theory]
    [InlineData(UserRole.Default, true)]
    [InlineData(UserRole.Ignored, false)]
    public void AnimeWisdom_CanExecute_ByRole(UserRole role, bool expected)
    {
        var cmd = new AnimeWisdomCommand(CommandTestMocks.CreateAskGeminiMock().Object);
        Assert.Equal(expected, cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name, role: role)));
    }

    [Fact]
    public async Task AnimeWisdom_HappyPath_EmptyParamsAllowed()
    {
        var gemini = CommandTestMocks.CreateAskGeminiMock();
        gemini.Setup(g => g.AskAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<double>()))
            .ReturnsAsync("\"Цитата\" – Персонаж");
        var cmd = new AnimeWisdomCommand(gemini.Object);

        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name, ""));

        Assert.Contains("Цитата", result);
    }
}
