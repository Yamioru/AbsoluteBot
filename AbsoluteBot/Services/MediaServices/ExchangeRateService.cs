using System.Globalization;
using AbsoluteBot.Services.UtilityServices;
using ImageChartsLib;
using Newtonsoft.Json.Linq;
using Serilog;

namespace AbsoluteBot.Services.MediaServices;

#pragma warning disable IDE0305
/// <summary>
///     Сервис для получения и обработки курсов валют, а также для создания графиков изменения курсов.
/// </summary>
public class ExchangeRateService(HttpClient httpClient, ConfigService configService) : IAsyncInitializable
{
    private const string BaseUrl = "https://api.apilayer.com/exchangerates_data/";
    private const string DateFormat = "yyyy-MM-dd";
    private const string ChartSize = "800x300";
    private const string ChartLegend = "Доллар|Евро";
    private const string ChartTitle = "Курс евро и доллара за последнюю неделю";
    private const double DollarNormalizationFactor = 0.6;
    private const double EuroNormalizationFactor = 0.3;

    public async Task InitializeAsync()
    {
        var apiKey = await configService.GetConfigValueAsync<string>("ExchangeApiKey").ConfigureAwait(false);
        httpClient.DefaultRequestHeaders.Add("apikey", apiKey);
        if (string.IsNullOrEmpty(apiKey)) Log.Warning("Не удалось загрузить api ключ для подключения к сервису ExchangeRateService.");
    }

    /// <summary>
    ///     Получает курсы валют за последние 7 дней.
    /// </summary>
    /// <returns>Словарь с датами и курсами валют (USD и EUR).</returns>
    public async Task<Dictionary<DateTime, (double? USD, double? EUR)>?> FetchExchangeRatesForLastWeek()
    {
        var startDate = DateTime.Now.AddDays(-7);
        var exchangeRates = new Dictionary<DateTime, (double? USD, double? EUR)>();

        // Цикл для получения курсов валют за последние 7 дней
        for (var i = 0; i < 7; i++)
        {
            var date = startDate.AddDays(i);

            // Получение курса валют на определенную дату
            var rateData = await FetchExchangeRatesForDate(date).ConfigureAwait(false);
            if (rateData != null)
                exchangeRates[date] = rateData.Value;
            else
                Log.Warning("Не удалось получить курс валют на дату {Date}", date);
        }

        // Возвращение полученных курсов или null, если данные отсутствуют
        return exchangeRates.Count > 0 ? exchangeRates : null;
    }

    /// <summary>
    ///     Получает URL графика изменения курса валют за последнюю неделю.
    /// </summary>
    /// <returns>URL графика курса валют или <c>null</c> в случае ошибки.</returns>
    public async Task<string?> GetExchangeRateChartUrlAsync()
    {
        try
        {
            // Получение курсов валют за последние 7 дней
            var exchangeRates = await FetchExchangeRatesForLastWeek().ConfigureAwait(false);
            if (exchangeRates == null)
                return null;

            // Генерация URL для графика
            var chartUrl = GenerateChartUrl(exchangeRates);
            return chartUrl;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при создании графика курса валют.");
            return null;
        }
    }

    /// <summary>
    ///     Метод для создания URL графика с использованием данных курсов валют.
    /// </summary>
    /// <param name="dollarProcessed">Нормализованные данные для курса доллара.</param>
    /// <param name="euroProcessed">Нормализованные данные для курса евро.</param>
    /// <param name="dataChl">Подписи курсов валют для графика.</param>
    /// <param name="dates">Форматированные даты для оси X графика.</param>
    /// <returns>URL графика.</returns>
    private static string CreateChartUrl(List<double> dollarProcessed, List<double> euroProcessed, string dataChl,
        string dates)
    {
        // Формирование данных для отображения на графике
        var data = $"{string.Join(";", dollarProcessed)}|{string.Join(";", euroProcessed)}"
            .Replace(",", ".").Replace(";", ",");

        // Генерация URL графика с помощью ImageCharts
        return new ImageCharts()
            .cht("ls")
            .chl(dataChl)
            .chd("t:" + data)
            .chxt("x")
            .chxl("0:|" + dates)
            .chs(ChartSize)
            .chdl(ChartLegend)
            .chdlp("t")
            .chf("b0,lg,90,0000FF,1,0000FF,0.2|b1,lg,90,FF0000,1,FF0000,0.2")
            .chlps("align,top|offset,5")
            .chm("o,ff0000,1,-1,5.0|o,0000ff,0,-1,5.0")
            .chtt(ChartTitle)
            .toURL();
    }

    /// <summary>
    ///     Получает курсы валют на определенную дату.
    /// </summary>
    /// <param name="date">Дата, для которой необходимо получить курсы.</param>
    /// <returns>Кортеж с курсами USD и EUR или <c>null</c> в случае ошибки.</returns>
    private async Task<(double? USD, double? EUR)?> FetchExchangeRatesForDate(DateTime date)
    {
        var formattedDate = date.ToString(DateFormat);

        // Отправка запроса на API для получения курсов валют
        var response = await httpClient.GetAsync($"{BaseUrl}{formattedDate}?symbols=EUR%2CUSD&base=RUB").ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            return null;

        // Обработка содержимого ответа
        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var json = JObject.Parse(content);

        var usdRate = json["rates"]?["USD"]?.ToObject<double>();
        var eurRate = json["rates"]?["EUR"]?.ToObject<double>();

        return (usdRate, eurRate);
    }

    /// <summary>
    ///     Метод для генерации URL графика изменений курсов валют.
    /// </summary>
    /// <param name="exchangeRates">Словарь с датами и курсами USD и EUR.</param>
    /// <returns>URL графика.</returns>
    private static string GenerateChartUrl(Dictionary<DateTime, (double? USD, double? EUR)> exchangeRates)
    {
        // Подготовка данных для графика
        var (dollarProcessed, euroProcessed) = PrepareChartData(exchangeRates);

        // Получение подписей и дат для графика
        var dataChl = GetChartLabels(exchangeRates);
        var dates = GetFormattedDates(exchangeRates.Keys.ToList());

        // Генерация и возвращение URL графика
        return CreateChartUrl(dollarProcessed, euroProcessed, dataChl, dates);
    }

    /// <summary>
    ///     Метод для создания подписей данных для графика.
    /// </summary>
    /// <param name="exchangeRates">Словарь с датами и курсами USD и EUR.</param>
    /// <returns>Строка с подписями курсов валют для графика.</returns>
    private static string GetChartLabels(Dictionary<DateTime, (double? USD, double? EUR)> exchangeRates)
    {
        var dollarRates = exchangeRates.Values
            .Select(r => r.USD.HasValue ? Math.Round(1 / r.USD.Value, 2).ToString(CultureInfo.InvariantCulture) : "0").ToList();
        var euroRates = exchangeRates.Values
            .Select(r => r.EUR.HasValue ? Math.Round(1 / r.EUR.Value, 2).ToString(CultureInfo.InvariantCulture) : "0").ToList();
        return $"{string.Join("|", dollarRates)}|{string.Join("|", euroRates)}";
    }

    /// <summary>
    ///     Метод для форматирования списка дат для отображения на оси X графика.
    /// </summary>
    /// <param name="dates">Список дат.</param>
    /// <returns>Форматированная строка дат для графика.</returns>
    private static string GetFormattedDates(List<DateTime> dates)
    {
        return string.Join("|", dates.Select(d => d.AddHours(3).ToShortDateString()));
    }

    /// <summary>
    ///     Метод для нормализации курсов валют перед отображением на графике.
    /// </summary>
    /// <param name="rates">Список курсов валют.</param>
    /// <param name="height">Коэффициент для нормализации значений.</param>
    /// <returns>Список нормализованных курсов валют.</returns>
    private static List<double> NormalizeRates(List<string> rates, double height)
    {
        var baseRate = Convert.ToDouble(rates[0]);
        return rates.Select(rate => Math.Round(1 + (Convert.ToDouble(rate) / baseRate - 1) * 2 - height, 3)).ToList();
    }

    /// <summary>
    ///     Метод для подготовки данных курсов валют для отображения на графике.
    /// </summary>
    /// <param name="exchangeRates">Словарь с датами и курсами USD и EUR.</param>
    /// <returns>Кортеж списков нормализованных данных для отображения на графике.</returns>
    private static (List<double> dollarProcessed, List<double> euroProcessed) PrepareChartData(
        Dictionary<DateTime, (double? USD, double? EUR)> exchangeRates)
    {
        // Преобразование курсов валют в списки строк
        var dollarRates = exchangeRates.Values
            .Select(r => r.USD.HasValue ? Math.Round(1 / r.USD.Value, 2).ToString(CultureInfo.InvariantCulture) : "0").ToList();
        var euroRates = exchangeRates.Values
            .Select(r => r.EUR.HasValue ? Math.Round(1 / r.EUR.Value, 2).ToString(CultureInfo.InvariantCulture) : "0").ToList();

        // Нормализация данных для графика
        var dollarProcessed = NormalizeRates(dollarRates, DollarNormalizationFactor);
        var euroProcessed = NormalizeRates(euroRates, EuroNormalizationFactor);

        return (dollarProcessed, euroProcessed);
    }
}