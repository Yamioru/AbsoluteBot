using System.Text.Json;
using AbsoluteBot.Models;
using AbsoluteBot.Services;
using AbsoluteBot.Services.GoogleSheetsServices;
using AbsoluteBot.Services.UtilityServices;
using AbsoluteBotTests.Infrastructure;
using Moq;

namespace AbsoluteBotTests.Services;

[Collection("BeginData")]
[Trait("Category", "Unit")]
[Trait("Area", "Services")]
public class GameProgressServiceTests(BeginDataFixture fixture)
{
    [Fact]
    public async Task CalculateGameProgressAsync_ReturnsPercentage_FromSeededFile()
    {
        var progressPath = Path.Combine(fixture.TempRoot, "game_progress.json");
        var data = new Dictionary<string, GameProgress>
        {
            ["TestGame"] = new GameProgress
            {
                GameName = "TestGame",
                CurrentPlaytimeMinutes = 50,
                TotalPlaytimeMinutes = 100,
                LastLoggedPercentage = 50
            }
        };
        await File.WriteAllTextAsync(progressPath, JsonSerializer.Serialize(data));

        var config = new Mock<ConfigService>();
        config.Setup(c => c.GetConfigValueAsync<string>("LastGameName")).ReturnsAsync("TestGame");
        var sheetsBase = new GoogleSheetsBaseService(config.Object);
        var gameSheets = new GameGoogleSheetsService(sheetsBase);
        var hltb = new HowLongToBeatService(new HttpClient(new MockHttpMessageHandler()));
        var service = new GameProgressService(gameSheets, config.Object, hltb);
        await service.InitializeAsync();

        var percentage = await service.CalculateGameProgressAsync();

        Assert.Equal(50.0, percentage);
    }

    [Fact]
    public async Task CalculateGameProgressAsync_ReturnsMinusOne_WhenNoLastGame()
    {
        var config = new Mock<ConfigService>();
        config.Setup(c => c.GetConfigValueAsync<string>("LastGameName")).ReturnsAsync((string?)null);
        var sheetsBase = new GoogleSheetsBaseService(config.Object);
        var service = new GameProgressService(
            new GameGoogleSheetsService(sheetsBase),
            config.Object,
            new HowLongToBeatService(new HttpClient(new MockHttpMessageHandler())));
        await service.InitializeAsync();

        var percentage = await service.CalculateGameProgressAsync();

        Assert.Equal(-1, percentage);
    }

    [Fact]
    public async Task AddTimeToCurrentGameAsync_UpdatesCurrentPlaytime()
    {
        var progressPath = Path.Combine(fixture.TempRoot, "game_progress.json");
        var data = new Dictionary<string, GameProgress>
        {
            ["SeedGame"] = new GameProgress
            {
                GameName = "SeedGame",
                CurrentPlaytimeMinutes = 10,
                TotalPlaytimeMinutes = 200,
                LastLoggedPercentage = 0
            }
        };
        await File.WriteAllTextAsync(progressPath, JsonSerializer.Serialize(data));

        var config = new Mock<ConfigService>();
        config.Setup(c => c.GetConfigValueAsync<string>("LastGameName")).ReturnsAsync("SeedGame");
        var sheetsBase = new GoogleSheetsBaseService(config.Object);
        var service = new GameProgressService(
            new GameGoogleSheetsService(sheetsBase),
            config.Object,
            new HowLongToBeatService(new HttpClient(new MockHttpMessageHandler())));
        await service.InitializeAsync();

        await service.AddTimeToCurrentGameAsync("SeedGame", 5);

        var reloaded = JsonSerializer.Deserialize<Dictionary<string, GameProgress>>(
            await File.ReadAllTextAsync(progressPath));
        Assert.Equal(15, reloaded!["SeedGame"].CurrentPlaytimeMinutes);
    }
}
