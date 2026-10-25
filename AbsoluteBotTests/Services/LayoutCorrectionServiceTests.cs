using AbsoluteBot.Services.UtilityServices;
using AbsoluteBotTests.Infrastructure;

namespace AbsoluteBotTests.Services;

[Collection("BeginData")]
[Trait("Category", "Unit")]
[Trait("Area", "Services")]
public class LayoutCorrectionServiceTests(BeginDataFixture fixture)
{
    [Fact]
    public async Task CorrectLayoutIfNeeded_ConvertsWrongLayoutRussian()
    {
        var previous = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(fixture.TempRoot);
            var service = new LayoutCorrectionService();
            await service.InitializeAsync();

            var result = service.CorrectLayoutIfNeeded("ghbdtn");

            Assert.Equal("привет", result);
        }
        finally
        {
            Directory.SetCurrentDirectory(previous);
        }
    }

    [Fact]
    public async Task CorrectLayoutIfNeeded_LeavesCyrillicUnchanged()
    {
        var previous = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(fixture.TempRoot);
            var service = new LayoutCorrectionService();
            await service.InitializeAsync();

            var result = service.CorrectLayoutIfNeeded("привет мир");

            Assert.Equal("привет мир", result);
        }
        finally
        {
            Directory.SetCurrentDirectory(previous);
        }
    }
}
