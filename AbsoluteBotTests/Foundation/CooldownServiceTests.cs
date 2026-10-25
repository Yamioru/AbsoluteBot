using AbsoluteBot.Services.CommandManagementServices;
using AbsoluteBotTests.Infrastructure;

namespace AbsoluteBotTests.Foundation;

[Collection("BeginData")]
[Trait("Category", "Unit")]
[Trait("Area", "Foundation")]
public class CooldownServiceTests
{
    [Fact]
    public async Task Initialize_LoadsDurationsFromTemp()
    {
        var service = new CooldownService();
        await service.InitializeAsync();

        Assert.False(service.IsOnCooldown("Telegram"));
    }

    [Fact]
    public async Task SetLastUsed_EnforcesCooldownForPlatform()
    {
        var service = new CooldownService();
        await service.InitializeAsync();

        Assert.True(await service.SetCooldownAsync("WebText", "60"));
        Assert.False(service.IsOnCooldown("WebText"));

        Assert.True(service.SetLastUsed("WebText"));
        Assert.True(service.IsOnCooldown("WebText"));
    }

    [Fact]
    public async Task ZeroDuration_IsNeverOnCooldown()
    {
        var service = new CooldownService();
        await service.InitializeAsync();

        Assert.True(await service.SetCooldownAsync("Twitch", "0"));
        service.SetLastUsed("Twitch");
        Assert.False(service.IsOnCooldown("Twitch"));
    }
}
