using AbsoluteBot.Chat.Commands.UserCommands;
using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services;
using AbsoluteBot.Services.GoogleSearch;
using AbsoluteBot.Services.GoogleSheetsServices;
using AbsoluteBot.Services.UserManagementServices;
using AbsoluteBot.Services.UtilityServices;
using AbsoluteBotTests.Infrastructure;
using Moq;

namespace AbsoluteBotTests.Commands.User;

[Trait("Category", "Unit")]
[Trait("Area", "Commands")]
public class PersonalityAndGameCommandTests
{
    private static Mock<MbtiService> CreateMbtiMock() =>
        new(new HttpClient(), new GoogleSearchService(new HttpClient(), new ConfigService()));

    private static Mock<SbtiService> CreateSbtiMock() => new();

    private static Mock<GameProgressService> CreateProgressMock()
    {
        var sheets = new GameGoogleSheetsService(new GoogleSheetsBaseService(new ConfigService()));
        return new Mock<GameProgressService>(sheets, new ConfigService(), new HowLongToBeatService(new HttpClient()));
    }

    [Fact]
    public void Mbti_CanExecute_OfficialChannel()
    {
        var cmd = new MbtiCommand(CreateMbtiMock().Object);
        Assert.True(cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name)));
        Assert.False(cmd.CanExecute(new ParsedCommand(cmd.Name, "", ChatContextFactory.CreateTelegramContext(ChannelType.General), UserRole.Default)));
    }

    [Fact]
    public async Task Mbti_MissingParameters_ReturnsUsage()
    {
        var cmd = new MbtiCommand(CreateMbtiMock().Object);
        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name, ""));
        Assert.Contains("Использование", result);
    }

    [Fact]
    public async Task Mbti_HappyPath_GetForUser()
    {
        var mbti = CreateMbtiMock();
        mbti.Setup(m => m.GetMbtiForUser("bob")).Returns("INTJ");
        var cmd = new MbtiCommand(mbti.Object);

        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name, "bob"));

        Assert.Contains("INTJ", result);
    }

    [Fact]
    public void Sbti_CanExecute_OfficialChannel()
    {
        var cmd = new SbtiCommand(CreateSbtiMock().Object);
        Assert.True(cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name)));
        Assert.False(cmd.CanExecute(new ParsedCommand(cmd.Name, "", ChatContextFactory.CreateTelegramContext(ChannelType.General), UserRole.Default)));
    }

    [Fact]
    public async Task Sbti_MissingParameters_ReturnsUsage()
    {
        var cmd = new SbtiCommand(CreateSbtiMock().Object);
        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name, ""));
        Assert.Contains("Использование", result);
    }

    [Fact]
    public async Task Sbti_HappyPath_GetForUser()
    {
        var sbti = CreateSbtiMock();
        sbti.Setup(s => s.GetSbtiForUser("bob")).Returns("NPC");
        var cmd = new SbtiCommand(sbti.Object);

        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name, "bob"));

        Assert.Contains("NPC", result);
    }

    [Fact]
    public void WhoMbti_CanExecute_OfficialChannel()
    {
        var cmd = new WhoMbtiCommand(CreateMbtiMock().Object, new Mock<ConfigService>().Object,
            CommandTestMocks.CreateAskGeminiMock().Object, new Mock<JsonMbtiCharacterCatalogService>().Object);
        Assert.True(cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name)));
        Assert.False(cmd.CanExecute(new ParsedCommand(cmd.Name, "", ChatContextFactory.CreateTelegramContext(ChannelType.General), UserRole.Default)));
    }

    [Fact]
    public void WhoSbti_CanExecute_OfficialChannel()
    {
        var cmd = new WhoSbtiCommand(CreateSbtiMock().Object, new Mock<ConfigService>().Object,
            CommandTestMocks.CreateAskGeminiMock().Object, new Mock<JsonSbtiCharacterCatalogService>().Object);
        Assert.True(cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name)));
        Assert.False(cmd.CanExecute(new ParsedCommand(cmd.Name, "", ChatContextFactory.CreateTelegramContext(ChannelType.General), UserRole.Default)));
    }

    [Fact]
    public void GameProgress_CanExecute_OfficialChannel()
    {
        var cmd = new GameProgressCommand(CreateProgressMock().Object);
        Assert.True(cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name)));
        Assert.False(cmd.CanExecute(new ParsedCommand(cmd.Name, "", ChatContextFactory.CreateTelegramContext(ChannelType.General), UserRole.Default)));
    }

    [Fact]
    public async Task GameProgress_HappyPath()
    {
        var progress = CreateProgressMock();
        progress.Setup(p => p.GenerateProgressMessageAsync()).ReturnsAsync("Прогресс: 50%");
        var cmd = new GameProgressCommand(progress.Object);

        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name));

        Assert.Equal("Прогресс: 50%", result);
    }

    [Fact]
    public void GameInfo_CanExecute_OfficialChannel()
    {
        var sheets = new Mock<GameGoogleSheetsService>(new GoogleSheetsBaseService(new ConfigService()));
        var cmd = new GameInfoCommand(sheets.Object, new Mock<ConfigService>().Object);
        Assert.True(cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name)));
        Assert.False(cmd.CanExecute(new ParsedCommand(cmd.Name, "", ChatContextFactory.CreateTelegramContext(ChannelType.General), UserRole.Default)));
    }

    [Fact]
    public async Task GameInfo_HappyPath()
    {
        var sheets = new Mock<GameGoogleSheetsService>(new GoogleSheetsBaseService(new ConfigService()));
        sheets.Setup(s => s.FetchGameInfoAsync("Doom")).ReturnsAsync("Инфо об игре");
        var cmd = new GameInfoCommand(sheets.Object, new Mock<ConfigService>().Object);

        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name, "Doom"));

        Assert.Equal("Инфо об игре", result);
    }

    [Fact]
    public void GameRating_CanExecute_OfficialChannel()
    {
        var rates = new Mock<RateGoogleSheetsService>(new GoogleSheetsBaseService(new ConfigService()));
        var cmd = new GameRatingCommand(rates.Object, new Mock<ConfigService>().Object);
        Assert.True(cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name)));
        Assert.False(cmd.CanExecute(new ParsedCommand(cmd.Name, "", ChatContextFactory.CreateTelegramContext(ChannelType.General), UserRole.Default)));
    }

    [Fact]
    public async Task GameRating_MissingParameters_ReturnsUsage()
    {
        var rates = new Mock<RateGoogleSheetsService>(new GoogleSheetsBaseService(new ConfigService()));
        var cmd = new GameRatingCommand(rates.Object, new Mock<ConfigService>().Object);
        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name, ""));
        Assert.Contains("Использование", result);
    }

    [Fact]
    public async Task GameRating_HappyPath()
    {
        var config = new Mock<ConfigService>();
        config.Setup(c => c.GetConfigValueAsync<string>("LastGameName")).ReturnsAsync("Doom");
        var rates = new Mock<RateGoogleSheetsService>(new GoogleSheetsBaseService(new ConfigService()));
        rates.Setup(r => r.UpdateGameRatingAsync("8", "testuser", "Doom")).ReturnsAsync("Оценка принята");
        var cmd = new GameRatingCommand(rates.Object, config.Object);

        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name, "8"));

        Assert.Equal("Оценка принята", result);
    }

    [Fact]
    public void GoogleSearchNeural_CanExecute()
    {
        var cmd = new GoogleSearchWithNeuralNetworkCommand(
            Mock.Of<IGoogleSearchService>(),
            CommandTestMocks.CreateAskGeminiMock().Object,
            new Mock<WebContentService>(new HttpClient()).Object);
        Assert.True(cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name)));
        Assert.False(cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name, role: UserRole.Ignored)));
    }

    [Fact]
    public async Task GoogleSearchNeural_MissingParameters_ReturnsUsage()
    {
        var cmd = new GoogleSearchWithNeuralNetworkCommand(
            Mock.Of<IGoogleSearchService>(),
            CommandTestMocks.CreateAskGeminiMock().Object,
            new Mock<WebContentService>(new HttpClient()).Object);
        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name, ""));
        Assert.Contains("Использование", result);
    }
}
