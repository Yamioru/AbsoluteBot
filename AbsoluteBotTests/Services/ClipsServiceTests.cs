using AbsoluteBot.Models;
using AbsoluteBot.Services;
using AbsoluteBotTests.Infrastructure;

namespace AbsoluteBotTests.Services;

[Collection("BeginData")]
[Trait("Category", "Unit")]
[Trait("Area", "Services")]
public class ClipsServiceTests(BeginDataFixture fixture)
{
    [Fact]
    public async Task AddClip_AppearsInGetAllClips()
    {
        _ = fixture;
        var service = new ClipsService();
        await service.InitializeAsync();

        var clip = new Clip
        {
            Id = 999001,
            Url = "https://example.com/clip",
            Name = "TestClip",
            Description = "desc"
        };
        await service.AddClipAsync(clip);

        Assert.Contains(service.GetAllClips(), c => c.Id == 999001 && c.Name == "TestClip");
    }
}
