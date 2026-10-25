using AbsoluteBot.Services;
using AbsoluteBot.Services.UtilityServices;
using AbsoluteBotTests.Infrastructure;
using Moq;

namespace AbsoluteBotTests.Services;

[Collection("BeginData")]
[Trait("Category", "Unit")]
[Trait("Area", "Services")]
public class AutoTranslateServiceTests(BeginDataFixture fixture)
{
    [Fact]
    public async Task Toggle_EnablesAndDisablesUser()
    {
        _ = fixture;
        var translation = new Mock<TranslationService>(MockBehavior.Loose, new HttpClient(), new Mock<ConfigService>().Object);
        var service = new AutoTranslateService(translation.Object);
        await service.InitializeAsync();

        Assert.False(service.IsUserAutoTranslating("user1"));
        Assert.True(await service.ToggleUserAutoTranslateAsync("user1"));
        Assert.True(service.IsUserAutoTranslating("user1"));
        Assert.True(await service.ToggleUserAutoTranslateAsync("user1"));
        Assert.False(service.IsUserAutoTranslating("user1"));
    }

    [Fact]
    public async Task TranslateUserMessageAsync_ReturnsNull_WhenDisabled()
    {
        _ = fixture;
        var translation = new Mock<TranslationService>(MockBehavior.Loose, new HttpClient(), new Mock<ConfigService>().Object);
        var service = new AutoTranslateService(translation.Object);
        await service.InitializeAsync();

        var result = await service.TranslateUserMessageAsync("nobody", "hello");

        Assert.Null(result);
    }
}
