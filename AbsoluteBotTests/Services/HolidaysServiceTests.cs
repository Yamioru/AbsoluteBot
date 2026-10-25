using AbsoluteBot.Services;
using AbsoluteBotTests.Infrastructure;

namespace AbsoluteBotTests.Services;

[Collection("BeginData")]
[Trait("Category", "Unit")]
[Trait("Area", "Services")]
public class HolidaysServiceTests(BeginDataFixture fixture)
{
    [Fact]
    public async Task GetHoliday_ReturnsKnownHolidayOrDefault()
    {
        _ = fixture;
        var service = new HolidaysService();
        await service.InitializeAsync();

        var newYear = service.GetHoliday("01.01");
        Assert.False(string.IsNullOrWhiteSpace(newYear));
        Assert.NotEqual("Сегодня нет праздника.", newYear);

        var missing = service.GetHoliday("99.99");
        Assert.Equal("Сегодня нет праздника.", missing);
    }
}
