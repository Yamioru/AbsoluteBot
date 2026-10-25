using AbsoluteBot.Services.UtilityServices;
using AbsoluteBotTests.Infrastructure;

namespace AbsoluteBotTests.Foundation;

[Collection("BeginData")]
[Trait("Category", "Unit")]
[Trait("Area", "Foundation")]
public class CensorWordsServiceTests(BeginDataFixture fixture)
{
    [Fact]
    public async Task Initialize_LoadsWords_NotEmpty()
    {
        Assert.True(File.Exists(Path.Combine(fixture.TempRoot, "censor_words.json")));

        var service = new CensorWordsService();
        await service.InitializeAsync();

        var words = service.GetAllCensorWords();
        Assert.False(words.IsEmpty);
    }

    [Fact]
    public async Task AddAndRemove_WordRoundTripsInTemp()
    {
        var service = new CensorWordsService();
        await service.InitializeAsync();

        const string word = "testcensorword_absolutobot";
        Assert.True(await service.AddCensorWordAsync(word));
        Assert.Contains(word, service.GetAllCensorWords(), StringComparer.InvariantCultureIgnoreCase);

        Assert.True(await service.RemoveCensorWordAsync(word));
        Assert.DoesNotContain(word, service.GetAllCensorWords(), StringComparer.InvariantCultureIgnoreCase);
    }
}
