using AbsoluteBot.Services.MediaServices;
using AbsoluteBot.Services.UtilityServices;
using AbsoluteBotTests.Infrastructure;
using Moq;

namespace AbsoluteBotTests;

[Trait("Category", "Integration")]
[Trait("RequiresVpn", "true")]
[Trait("Area", "Services")]
[Collection("BeginData")]
public class ImageGeneratorServiceTests(BeginDataFixture fixture)
{
    [Fact]
    public async Task GenerateCommandsImageAsync_ReturnsUrl_OnSuccess()
    {
        var config = new ConfigService();
        await config.InitializeAsync();
        var service = new ImageGeneratorService(new HttpClient(), config);
        await service.InitializeAsync();

        var result = await service.GenerateCommandsImageAsync("Sample text");

        Assert.False(string.IsNullOrWhiteSpace(result));
        Assert.StartsWith("http", result!);
        _ = fixture;
    }

    [Fact]
    public async Task GenerateCommandsImageAsync_ReturnsNull_OnEmptyText()
    {
        var mockConfig = new Mock<ConfigService>();
        mockConfig.Setup(c => c.GetConfigValueAsync<string>("ImgApiKey")).ReturnsAsync("dummy");
        var service = new ImageGeneratorService(new HttpClient(), mockConfig.Object);
        await service.InitializeAsync();

        var result = await service.GenerateCommandsImageAsync("");

        Assert.Null(result);
    }
}
