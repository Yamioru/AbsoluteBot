using AbsoluteBot.Services.MediaServices;

namespace AbsoluteBotTests;

[Trait("Category", "Integration")]
[Trait("RequiresVpn", "true")]
[Trait("Area", "Services")]
public class VideoSearchServiceTests
{
    private readonly VideoSearchService _videoSearchService = new(new HttpClient());

    [Fact]
    public async Task SearchVideoAsync_ReturnsYoutubeUrls()
    {
        var videoUrls = new List<string>();
        for (var i = 0; i < 4; i++)
            videoUrls.Add(await _videoSearchService.SearchVideoAsync("test"));

        var distinct = videoUrls.Distinct().ToList();
        Assert.True(distinct.Count >= 1);
        Assert.All(distinct, url => Assert.StartsWith("https://youtu.be/", url));
    }
}
