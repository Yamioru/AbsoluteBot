using AbsoluteBot.Chat.Commands.UserCommands;
using AbsoluteBot.Models;
using AbsoluteBot.Services;
using AbsoluteBot.Services.GoogleSearch;
using AbsoluteBot.Services.NeuralNetworkServices;
using AbsoluteBotTests.Infrastructure;
using Moq;

namespace AbsoluteBotTests.Commands.User;

[Collection("BeginData")]
[Trait("Category", "Unit")]
[Trait("Area", "Commands")]
public class AskAndDefinitionCommandTests(BeginDataFixture fixture)
{
    [Theory]
    [InlineData(UserRole.Default, true)]
    [InlineData(UserRole.Ignored, false)]
    public void Ask_CanExecute_ByRole(UserRole role, bool expected)
    {
        var cmd = new AskCommand(CommandTestMocks.CreateChatGptMock().Object);
        Assert.Equal(expected, cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name, role: role)));
    }

    [Fact]
    public async Task Ask_HappyPath()
    {
        var gpt = CommandTestMocks.CreateChatGptMock();
        gpt.Setup(g => g.AskChatGptAsync("привет", It.IsAny<int>())).ReturnsAsync("ответ");
        var cmd = new AskCommand(gpt.Object);

        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name, "привет"));

        Assert.Equal("ответ", result);
    }

    [Fact]
    public async Task Ask_MissingParameters_ReturnsUsage()
    {
        var cmd = new AskCommand(CommandTestMocks.CreateChatGptMock().Object);
        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name));
        Assert.Contains("Использование", result);
    }

    [Theory]
    [InlineData(UserRole.Default, true)]
    [InlineData(UserRole.Ignored, false)]
    public void AskGemini_CanExecute_ByRole(UserRole role, bool expected)
    {
        var cmd = new AskGeminiCommand(CommandTestMocks.CreateAskGeminiMock().Object);
        Assert.Equal(expected, cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name, role: role)));
    }

    [Fact]
    public async Task AskGemini_HappyPath()
    {
        var gemini = CommandTestMocks.CreateAskGeminiMock();
        gemini.Setup(g => g.AskAsync("вопрос", It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<double>()))
            .ReturnsAsync("ответ gemini");
        var cmd = new AskGeminiCommand(gemini.Object);

        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name, "вопрос"));

        Assert.Equal("ответ gemini", result);
    }

    [Fact]
    public async Task AskGemini_MissingParameters_ReturnsUsage()
    {
        var cmd = new AskGeminiCommand(CommandTestMocks.CreateAskGeminiMock().Object);
        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name));
        Assert.Contains("Использование", result);
    }

    [Theory]
    [InlineData(UserRole.Default, true)]
    [InlineData(UserRole.Ignored, false)]
    public async Task AskGeminiGoogle_CanExecute_ByRole_WhenPaid(UserRole role, bool expected)
    {
        _ = fixture;
        var models = new NeuralModelConfigService();
        await models.InitializeAsync();
        await models.SetTierAsync(NeuralTiers.Paid);
        var cmd = new AskGeminiWithGoogleSearchCommand(CommandTestMocks.CreateAskGeminiMock().Object, models);
        Assert.Equal(expected, cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name, role: role)));
        await models.SetTierAsync(NeuralTiers.Free);
    }

    [Fact]
    public async Task AskGeminiGoogle_CanExecute_FalseWhenFree()
    {
        _ = fixture;
        var models = new NeuralModelConfigService();
        await models.InitializeAsync();
        await models.SetTierAsync(NeuralTiers.Free);
        var cmd = new AskGeminiWithGoogleSearchCommand(CommandTestMocks.CreateAskGeminiMock().Object, models);
        Assert.False(cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name, role: UserRole.Default)));
    }

    [Fact]
    public async Task AskGeminiGoogle_HappyPath()
    {
        var gemini = CommandTestMocks.CreateAskGeminiMock();
        gemini.Setup(g => g.AskWithSearchAsync(It.Is<string>(s => s.Contains("тест")), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string>(), It.IsAny<double>()))
            .ReturnsAsync("нашёл");
        var models = new NeuralModelConfigService();
        var cmd = new AskGeminiWithGoogleSearchCommand(gemini.Object, models);

        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name, "тест"));

        Assert.Equal("нашёл", result);
    }

    [Theory]
    [InlineData(UserRole.Default, true)]
    [InlineData(UserRole.Ignored, false)]
    public void AskMailRu_CanExecute_ByRole(UserRole role, bool expected)
    {
        var cmd = new AskMailRuCommand(new Mock<MailRuAnswerService>(new HttpClient()).Object);
        Assert.Equal(expected, cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name, role: role)));
    }

    [Fact]
    public async Task AskMailRu_HappyPath()
    {
        var mail = new Mock<MailRuAnswerService>(new HttpClient());
        mail.Setup(m => m.AskAsync("как дела")).ReturnsAsync("нормально");
        var cmd = new AskMailRuCommand(mail.Object);

        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name, "как дела"));

        Assert.Equal("нормально", result);
    }

    [Fact]
    public async Task AskMailRu_MissingParameters_ReturnsUsage()
    {
        var cmd = new AskMailRuCommand(new Mock<MailRuAnswerService>(new HttpClient()).Object);
        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name));
        Assert.Contains("Использование", result);
    }

    [Theory]
    [InlineData(UserRole.Default, true)]
    [InlineData(UserRole.Ignored, false)]
    public void Definition_CanExecute_ByRole(UserRole role, bool expected)
    {
        var google = new Mock<IGoogleSearchDefinitionService>();
        var cmd = new DefinitionCommand(google.Object, CommandTestMocks.CreateAskGeminiMock().Object);
        Assert.Equal(expected, cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name, role: role)));
    }

    [Fact]
    public async Task Definition_HappyPath()
    {
        var google = new Mock<IGoogleSearchDefinitionService>();
        google.Setup(g => g.GetDefinitionAsync("кот", It.IsAny<int>())).ReturnsAsync("животное");
        var cmd = new DefinitionCommand(google.Object, CommandTestMocks.CreateAskGeminiMock().Object);

        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name, "кот"));

        Assert.Equal("животное", result);
    }

    [Fact]
    public async Task Definition_MissingParameters_ReturnsUsage()
    {
        var google = new Mock<IGoogleSearchDefinitionService>();
        var cmd = new DefinitionCommand(google.Object, CommandTestMocks.CreateAskGeminiMock().Object);
        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name));
        Assert.Contains("Использование", result);
    }
}