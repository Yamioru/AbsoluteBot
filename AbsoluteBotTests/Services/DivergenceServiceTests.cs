using AbsoluteBot.Services;

namespace AbsoluteBotTests.Services;

[Trait("Category", "Unit")]
[Trait("Area", "Services")]
public class DivergenceServiceTests
{
    [Fact]
    public async Task GetCurrentDivergence_IsDeterministicAcrossInstances()
    {
        var a = new DivergenceService();
        var b = new DivergenceService();
        await a.InitializeAsync();
        await b.InitializeAsync();

        var valueA = a.GetCurrentDivergence();
        var valueB = b.GetCurrentDivergence();

        Assert.Equal(valueA, valueB);
        Assert.Matches(@"^\d+[.,]\d{6}$", valueA);
    }

    [Fact]
    public async Task GetCurrentDivergence_StableOnRepeatedCalls()
    {
        var service = new DivergenceService();
        await service.InitializeAsync();

        var first = service.GetCurrentDivergence();
        var second = service.GetCurrentDivergence();

        Assert.Equal(first, second);
    }
}
