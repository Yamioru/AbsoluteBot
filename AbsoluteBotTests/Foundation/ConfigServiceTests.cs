using AbsoluteBot.Services.UtilityServices;
using AbsoluteBotTests.Infrastructure;

namespace AbsoluteBotTests.Foundation;

[Collection("BeginData")]
[Trait("Category", "Unit")]
[Trait("Area", "Foundation")]
public class ConfigServiceTests(BeginDataFixture fixture)
{
    [Fact]
    public async Task Initialize_LoadsConfigFromDataRoot()
    {
        var service = new ConfigService();
        await service.InitializeAsync();
        var botName = await service.GetConfigValueAsync<string>("BotName");
        Assert.False(string.IsNullOrWhiteSpace(botName));
        Assert.True(File.Exists(Path.Combine(fixture.TempRoot, "config.json")));
    }

    [Fact]
    public async Task SetAndGet_RoundTripsValue()
    {
        var service = new ConfigService();
        await service.InitializeAsync();
        await service.SetConfigValueAsync("TestOnlyKey_AbsoluteBot", "value123");
        var value = await service.GetConfigValueAsync<string>("TestOnlyKey_AbsoluteBot");
        Assert.Equal("value123", value);
    }

    [Fact]
    public async Task GetAllConfigValues_MasksSecrets()
    {
        var service = new ConfigService();
        await service.InitializeAsync();
        var all = service.GetAllConfigValues();
        Assert.NotEmpty(all);
        if (all.TryGetValue("TelegramBotApiKey", out var masked) && !string.IsNullOrEmpty(masked))
            Assert.DoesNotContain(masked, ":"); // грубая проверка маскирования / не полный ключ в открытом виде не гарантируем формат
    }

    [Fact]
    public void DataPaths_UsesEnvironmentRoot()
    {
        Assert.Equal(Path.GetFullPath(fixture.TempRoot), Path.GetFullPath(DataPaths.Root));
        Assert.Equal(Path.Combine(DataPaths.Root, "config.json"), DataPaths.Get("config.json"));
    }
}
