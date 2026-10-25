using AbsoluteBot.Services.CommandManagementServices;
using AbsoluteBotTests.Infrastructure;

namespace AbsoluteBotTests.Foundation;

[Collection("BeginData")]
[Trait("Category", "Unit")]
[Trait("Area", "Foundation")]
public class CommandStatusServiceTests
{
    [Fact]
    public async Task Initialize_LoadsStatusesFromCommandsJson()
    {
        var service = new CommandStatusService();
        await service.InitializeAsync();

        var statuses = service.GetAllCommandStatuses();
        Assert.NotEmpty(statuses);
    }

    [Fact]
    public async Task SetCommandStatus_DisableThenEnable()
    {
        var service = new CommandStatusService();
        await service.InitializeAsync();

        const string command = "!факт";
        const string platform = "WebText";

        Assert.True(await service.SetCommandStatusAsync(command, platform, false));
        Assert.False(await service.IsCommandEnabled(command, platform));

        Assert.True(await service.SetCommandStatusAsync(command, platform, true));
        Assert.True(await service.IsCommandEnabled(command, platform));
    }
}
