using AbsoluteBot.Services.GoogleSearch;

namespace AbsoluteBotTests;

[Trait("Category", "Integration")]
[Trait("RequiresVpn", "true")]
[Trait("Area", "Services")]
public class GoogleSearchDefinitionServiceTests
{
    private readonly GoogleSearchDefinitionService _service = new(new HttpClient());

    [Theory]
    [InlineData("кранч", 100)]
    [InlineData("горазд", 100)]
    [InlineData("смерть", 150)]
    public async Task GetDefinitionAsync_ReturnsNonEmptyDefinition(string query, int maxLength)
    {
        var actual = await _service.GetDefinitionAsync(query, maxLength);

        Assert.False(string.IsNullOrWhiteSpace(actual));
        Assert.True(actual!.Length <= maxLength + 50);
    }
}
