using AbsoluteBot.Services.CommandManagementServices;
using AbsoluteBotTests.Infrastructure;

namespace AbsoluteBotTests.Foundation;

[Collection("BeginData")]
[Trait("Category", "Unit")]
[Trait("Area", "Foundation")]
public class CommandFileServiceTests(BeginDataFixture fixture)
{
    [Fact]
    public async Task LoadCommandStatusesAsync_LoadsFromTempCommandsJson()
    {
        Assert.True(File.Exists(Path.Combine(fixture.TempRoot, "commands.json")));

        var commands = await CommandFileService.LoadCommandStatusesAsync();

        Assert.NotEmpty(commands);
        Assert.True(commands.ContainsKey("!факт") || commands.Keys.Any(k => k.Contains("факт")));
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripsStatus()
    {
        var data = await CommandFileService.LoadCommandStatusesAsync();
        data["!testcmd_absolutobot"] = new Dictionary<string, bool> { ["WebText"] = false };

        await CommandFileService.SaveCommandStatusesAsync(data);
        var loaded = await CommandFileService.LoadCommandStatusesAsync();

        Assert.True(loaded.TryGetValue("!testcmd_absolutobot", out var platforms));
        Assert.False(platforms!["WebText"]);
        Assert.True(loaded.Count > 1); // не затираем остальные команды в общей temp-копии
    }
}
