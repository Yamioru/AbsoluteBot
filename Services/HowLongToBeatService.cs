using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AbsoluteBot.Helpers;
using AbsoluteBot.Models;
using HtmlAgilityPack;
using Serilog;

namespace AbsoluteBot.Services;

/// <summary>
/// Сервис для получения информации о времени прохождения игр с сайта HowLongToBeat.
/// </summary>
public partial class HowLongToBeatService
{
    private const string SearchUrlTemplate = "https://howlongtobeat.com/api/seek/";
    private const string BaseUrl = "https://howlongtobeat.com";
    private const string AcceptLanguageHeader = "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7";
    private const string AcceptHeader = "*/*";
    private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36";
    private const string OriginHeader = "https://howlongtobeat.com";
    private const string RefererHeader = "https://howlongtobeat.com";
    private readonly HttpClient _httpClient;

    public HowLongToBeatService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);
        _httpClient.DefaultRequestHeaders.Add("Accept", AcceptHeader);
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", AcceptLanguageHeader);
        _httpClient.DefaultRequestHeaders.Add("Origin", OriginHeader);
        _httpClient.DefaultRequestHeaders.Add("Referer", RefererHeader);
    }

    /// <summary>
    /// Получает предполагаемое время прохождения игры.
    /// </summary>
    /// <param name="gameName">Название игры.</param>
    /// <returns>Время прохождения игры в минутах.</returns>
    public async Task<int> GetGameDurationAsync(string gameName)
    {
        try
        {
            var cleanedGameName = CleanGameName(gameName);
            var searchKey = await FetchSearchKeyAsync().ConfigureAwait(false);

            if (string.IsNullOrEmpty(searchKey)) return 0;

            var searchUrl = BuildSearchUrl(searchKey);
            var searchRequest = BuildSearchRequest(cleanedGameName);

            var gameData = await FetchGameDataAsync(searchUrl, searchRequest).ConfigureAwait(false);

            return ExtractGameDuration(gameData);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Ошибка при получении времени прохождения игры '{gameName}' из HowLongToBeat.");
            return 0;
        }
    }

    /// <summary>
    /// Создает запрос на основе названия игры.
    /// </summary>
    /// <param name="gameName">Название игры.</param>
    /// <returns>Объект запроса для поиска игры.</returns>
    private static object BuildSearchRequest(string gameName)
    {
        return new
        {
            searchType = "games",
            searchTerms = gameName.Split(' '),
            searchPage = 1,
            size = 1,
            searchOptions = new
            {
                games = new
                {
                    userId = 0,
                    platform = "",
                    sortCategory = "popular",
                    rangeCategory = "main",
                    rangeTime = new {min = (int?) null, max = (int?) null},
                    gameplay = new {perspective = "", flow = "", genre = "", difficulty = ""},
                    rangeYear = new {min = "", max = ""},
                    modifier = ""
                },
                users = new
                {
                    sortCategory = "postcount"
                },
                lists = new
                {
                    sortCategory = "follows"
                },
                filter = "",
                sort = 0,
                randomizer = 0
            },
            useCache = false
        };
    }

    /// <summary>
    /// Формирует URL для запроса с использованием поискового ключа.
    /// </summary>
    /// <param name="searchKey">Поисковый ключ.</param>
    /// <returns>URL для выполнения поиска игры.</returns>
    private static string BuildSearchUrl(string searchKey)
    {
        return $"{SearchUrlTemplate}{searchKey}";
    }

    /// <summary>
    /// Очищает название игры от неалфавитных символов.
    /// </summary>
    /// <param name="gameName">Название игры.</param>
    /// <returns>Очищенное название игры.</returns>
    private static string CleanGameName(string gameName)
    {
        return TextProcessingUtils.RemoveNonAlphanumericCharacters(gameName);
    }

    /// <summary>
    /// Извлекает продолжительность прохождения игры из данных поиска.
    /// </summary>
    /// <param name="gameData">Ответ от API с данными об игре.</param>
    /// <returns>Продолжительность прохождения в минутах.</returns>
    private static int ExtractGameDuration(HowLongToBeatSearchResponse? gameData)
    {
        if (!(gameData?.Data.Count > 0)) return 0;

        // Берется значение не равное нулю первое среди Comp100->CompPlus->CompMain или 0 если не найдено
        var game = gameData.Data[0];
        var completionTime = game.Comp100 > 0
            ? game.Comp100
            : game.CompPlus > 0
                ? game.CompPlus
                : game.CompMain;

        return completionTime == 0 ? 0 : completionTime / 60;
    }

    /// <summary>
    /// Извлекает URL скрипта из HTML-контента.
    /// </summary>
    /// <param name="htmlContent">HTML-контент страницы.</param>
    /// <returns>URL скрипта или null, если не найдено.</returns>
    private static string? ExtractScriptUrl(string htmlContent)
    {
        var htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(htmlContent);

        var scriptNodes = htmlDocument.DocumentNode.SelectNodes("//script[@src]");
        return (from scriptNode in scriptNodes
            select scriptNode.GetAttributeValue("src", string.Empty)
            into src
            where src.Contains("_app-")
            select BaseUrl + src).FirstOrDefault();
    }

    /// <summary>
    /// Выполняет запрос к API HowLongToBeat для получения данных об игре.
    /// </summary>
    /// <param name="searchUrl">URL для поиска.</param>
    /// <param name="searchRequest">Запрос для поиска игры.</param>
    /// <returns>Данные о результатах поиска игры.</returns>
    private async Task<HowLongToBeatSearchResponse?> FetchGameDataAsync(string searchUrl, object searchRequest)
    {
        var content = new StringContent(JsonSerializer.Serialize(searchRequest), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(searchUrl, content).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var jsonResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return JsonSerializer.Deserialize<HowLongToBeatSearchResponse>(jsonResponse);
    }

    /// <summary>
    /// Получает поисковый ключ из скрипта по указанному URL.
    /// </summary>
    /// <param name="scriptUrl">URL скрипта.</param>
    /// <returns>Поисковый ключ или null, если не найдено.</returns>
    private async Task<string?> FetchKeyFromScriptAsync(string scriptUrl)
    {
        var scriptResponse = await _httpClient.GetStringAsync(scriptUrl).ConfigureAwait(false);

        var match = SearchKeyRegex().Match(scriptResponse);
        if (match.Success) return match.Groups["key"].Value;

        // Попытка найти ключ с использованием второго регулярного выражения
        match = SearchKeyRegex2().Match(scriptResponse);
        if (match.Success) return match.Groups["part1"].Value + match.Groups["part2"].Value;

        match = SearchKeyRegex3().Match(scriptResponse);
        if (match.Success) return match.Groups["part1"].Value + match.Groups["part2"].Value;

        // Возврат null, если ключ не найден
        return null;
    }

    /// <summary>
    /// Получает поисковый ключ, необходимый для выполнения запросов к API HowLongToBeat.
    /// </summary>
    /// <returns>Поисковый ключ.</returns>
    private async Task<string?> FetchSearchKeyAsync()
    {
        try
        {
            var response = await _httpClient.GetStringAsync(BaseUrl).ConfigureAwait(false);
            var scriptUrl = ExtractScriptUrl(response);

            if (string.IsNullOrEmpty(scriptUrl)) return null;

            return await FetchKeyFromScriptAsync(scriptUrl).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при получении поискового ключа для HowLongToBeat.");
            return null;
        }
    }

    [GeneratedRegex("\"/api/search/\"\\.concat\\(\"([a-zA-Z0-9]+)\"\\)", RegexOptions.Compiled)]
    private static partial Regex SearchKeyRegex();

    [GeneratedRegex("\"/api/find/\"\\.concat\\(\"(?<part1>[a-zA-Z0-9]+)\"\\)\\.concat\\(\"(?<part2>[a-zA-Z0-9]+)\"\\)", RegexOptions.Compiled)]
    private static partial Regex SearchKeyRegex2();

    [GeneratedRegex("\"/api/seek/\"\\.concat\\(\"(?<part1>[a-zA-Z0-9]+)\"\\)\\.concat\\(\"(?<part2>[a-zA-Z0-9]+)\"\\)", RegexOptions.Compiled)]
    private static partial Regex SearchKeyRegex3();
}