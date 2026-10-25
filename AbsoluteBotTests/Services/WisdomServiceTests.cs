using AbsoluteBot.Models;
using AbsoluteBot.Services;
using AbsoluteBotTests.Infrastructure;

namespace AbsoluteBotTests.Services;

[Collection("BeginData")]
[Trait("Category", "Unit")]
[Trait("Area", "Services")]
public class WisdomServiceTests(BeginDataFixture fixture)
{
    [Fact]
    public async Task AddAndGetRandomWisdom_RoundTrips()
    {
        _ = fixture;
        var service = new WisdomService();
        await service.InitializeAsync();

        var added = await service.AddWisdomAsync("Тестовая мудрость AbsoluteBot");
        Assert.True(added);

        var wisdom = await service.GetRandomWisdomAsync();
        Assert.False(string.IsNullOrWhiteSpace(wisdom));
    }
}
