using AbsoluteBot.Services.UtilityServices;
using AbsoluteBotTests.Infrastructure;

namespace AbsoluteBotTests.Services;

[Collection("BeginData")]
[Trait("Category", "Unit")]
[Trait("Area", "Services")]
public class BotUpdateServiceTests(BeginDataFixture fixture)
{
    [Fact]
    public async Task RequestUpdateAsync_WritesFlagInDataRoot()
    {
        var service = new BotUpdateService();
        await service.RequestUpdateAsync();

        var path = Path.Combine(fixture.TempRoot, BotUpdateService.RequestFileName);
        Assert.True(File.Exists(path));
        Assert.False(string.IsNullOrWhiteSpace(await File.ReadAllTextAsync(path)));
    }
}
