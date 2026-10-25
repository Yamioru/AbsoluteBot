using AbsoluteBot.Services.GoogleSheetsServices;
using AbsoluteBot.Services.UtilityServices;
using AbsoluteBotTests.Infrastructure;

namespace AbsoluteBotTests.Integration;

[Collection("BeginData")]
[Trait("Category", "Integration")]
[Trait("LiveSheets", "true")]
[Trait("Area", "Services")]
public class GoogleSheetsLiveTests(BeginDataFixture fixture)
{
    [Fact]
    public async Task Initialize_ConnectsToTestSpreadsheet()
    {
        var testSheetId = Environment.GetEnvironmentVariable("TEST_GOOGLE_SHEETS_ID");
        if (string.IsNullOrWhiteSpace(testSheetId))
            return;

        _ = fixture;
        var config = new ConfigService();
        await config.InitializeAsync();
        await config.SetConfigValueAsync("GoogleSheetsId", testSheetId);

        var sheets = new GoogleSheetsBaseService(config);
        await sheets.InitializeAsync();

        Assert.NotNull(sheets.SheetsService);
        Assert.Equal(testSheetId, sheets.SheetsId);

        // Soft read of first sheet metadata / values if available
        var values = await sheets.GetValuesAsync("A1:A1");
        Assert.NotNull(values);
    }
}
