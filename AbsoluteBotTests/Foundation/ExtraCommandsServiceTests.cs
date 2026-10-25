using AbsoluteBot.Services.CommandManagementServices;
using AbsoluteBotTests.Infrastructure;

namespace AbsoluteBotTests.Foundation;

[Collection("BeginData")]
[Trait("Category", "Unit")]
[Trait("Area", "Foundation")]
public class ExtraCommandsServiceTests(BeginDataFixture fixture)
{
    [Fact]
    public async Task Initialize_LoadsFromTempExtraCommandsJson()
    {
        Assert.True(File.Exists(Path.Combine(fixture.TempRoot, "extra_commands.json")));

        var service = new ExtraCommandsService();
        await service.InitializeAsync();

        var commands = service.GetAllCommands().ToList();
        Assert.NotEmpty(commands);
        Assert.NotNull(service.GetCommand("!discord"));
    }

    [Fact]
    public async Task AddOrUpdateAndRemove_RoundTripsInTemp()
    {
        var service = new ExtraCommandsService();
        await service.InitializeAsync();

        const string name = "!testextra_absolutobot";
        Assert.True(await service.AddOrUpdateCommandAsync(name, "hello-extra"));
        Assert.Equal("hello-extra", service.GetCommand(name));

        Assert.True(await service.RemoveCommandAsync(name));
        Assert.Null(service.GetCommand(name));
    }
}
