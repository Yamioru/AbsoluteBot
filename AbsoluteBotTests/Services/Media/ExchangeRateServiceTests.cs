using System.Globalization;
using System.Net;
using AbsoluteBot.Services.MediaServices;
using AbsoluteBot.Services.UtilityServices;
using AbsoluteBotTests.Infrastructure;
using Moq;

namespace AbsoluteBotTests.Services.Media;

[Trait("Category", "Unit")]
[Trait("Area", "Services")]
public class ExchangeRateServiceTests
{
    private static string RateJson(double usd, double eur)
    {
        var u = usd.ToString(CultureInfo.InvariantCulture);
        var e = eur.ToString(CultureInfo.InvariantCulture);
        return "{\"rates\":{\"USD\":" + u + ",\"EUR\":" + e + "}}";
    }

    [Fact]
    public async Task FetchExchangeRatesForLastWeek_ReturnsSevenDays()
    {
        var handler = new MockHttpMessageHandler();
        for (var i = 0; i < 7; i++)
            handler.EnqueueResponse(HttpStatusCode.OK, RateJson(0.01 + i * 0.001, 0.009 + i * 0.001));

        var config = new Mock<ConfigService>();
        config.Setup(c => c.GetConfigValueAsync<string>("ExchangeApiKey")).ReturnsAsync("key");
        var service = new ExchangeRateService(new HttpClient(handler, disposeHandler: false), config.Object);
        await service.InitializeAsync();

        var rates = await service.FetchExchangeRatesForLastWeek();

        Assert.NotNull(rates);
        Assert.Equal(7, rates!.Count);
        Assert.Equal(7, handler.Requests.Count);
    }

    [Fact]
    public async Task GetExchangeRateChartUrlAsync_ContainsImageChartsHost()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");

        try
        {
            var handler = new MockHttpMessageHandler();
            for (var i = 0; i < 14; i++)
                handler.EnqueueResponse(HttpStatusCode.OK, RateJson(0.01 + (i % 7) * 0.001, 0.009 + (i % 7) * 0.001));

            var config = new Mock<ConfigService>();
            config.Setup(c => c.GetConfigValueAsync<string>("ExchangeApiKey")).ReturnsAsync("key");
            var service = new ExchangeRateService(new HttpClient(handler, disposeHandler: false), config.Object);
            await service.InitializeAsync();

            var rates = await service.FetchExchangeRatesForLastWeek();
            Assert.NotNull(rates);
            Assert.Equal(7, rates!.Count);

            var url = await service.GetExchangeRateChartUrlAsync();

            Assert.NotNull(url);
            Assert.Contains("image-charts.com", url!, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(14, handler.Requests.Count);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }
}