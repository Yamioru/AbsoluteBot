using AbsoluteBot.Services.MediaServices;

namespace AbsoluteBotTests;

[Trait("Category", "Integration")]
[Trait("RequiresVpn", "true")]
[Trait("Area", "Services")]
public class ImageSearchServiceTests
{
    private readonly ImageSearchService _imageSearchService = new(new HttpClient());

    [Fact]
    public async Task SearchImageAsync_ReturnsHttpUrl_WhenValidSearchTextIsProvided()
    {
        var result = await _imageSearchService.SearchImageAsync("котик");

        Assert.StartsWith("http", result);
        Assert.False(string.IsNullOrWhiteSpace(result));
    }

    [Fact]
    public async Task SearchImageAsync_ReturnsPlaceholderOrUrl_OnDifficultQuery()
    {
        var result = await _imageSearchService.SearchImageAsync("República Mayor de Centro América flag");

        Assert.StartsWith("http", result);
    }
}
