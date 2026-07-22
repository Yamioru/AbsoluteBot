using AbsoluteBot.Helpers;
using AbsoluteBot.Models;
using Serilog;
using System.Drawing.Drawing2D;
using System.Text;
using System.Text.Json;

namespace AbsoluteBot.Services;

/// <summary>
/// Сервис для получения информации о времени прохождения игр с сайта HowLongToBeat.
/// </summary>
public class HowLongToBeatService
{
    private const string SearchUrl = "https://howlongtobeat.com/api/find/";
    private const string SearchInitUrl = "https://howlongtobeat.com/api/find/init";
    private const string BaseUrl = "https://howlongtobeat.com";
    private const string AcceptLanguageHeader = "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7";
    private const string AcceptHeader = "*/*";
    private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/144.0.0.0 Safari/537.36";
    private const string OriginHeader = "https://howlongtobeat.com";
    private const string RefererHeader = "https://howlongtobeat.com";
    private readonly HttpClient _httpClient;
    private record AuthData(string? Token, string? HpKey, string? HpVal);
    public HowLongToBeatService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);
        _httpClient.DefaultRequestHeaders.Add("Accept", AcceptHeader);
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", AcceptLanguageHeader);
        _httpClient.DefaultRequestHeaders.Add("Origin", OriginHeader);
        _httpClient.DefaultRequestHeaders.Add("Referer", RefererHeader);
        _httpClient.DefaultRequestHeaders.Add("Cookie", "pv=1");
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
            var authData = await FetchAuthDataAsync().ConfigureAwait(false);

            if (string.IsNullOrEmpty(authData?.Token)) return 0;

            var searchRequest = BuildSearchRequest(cleanedGameName, authData.HpKey, authData.HpVal);

            var gameData = await FetchGameDataAsync(searchRequest, authData).ConfigureAwait(false);

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
    private static object BuildSearchRequest(string gameName, string? hpKey, string? hpVal)
    {
        var request = new Dictionary<string, object>
        {
            ["searchType"] = "games",
            ["searchTerms"] = gameName.Split(' '),
            ["searchPage"] = 1,
            ["size"] = 1,
            ["searchOptions"] = new
            {
                games = new
                {
                    userId = 0,
                    platform = "",
                    sortCategory = "popular",
                    rangeCategory = "main",
                    rangeTime = new { min = (int?)null, max = (int?)null },
                    gameplay = new { perspective = "", flow = "", genre = "", difficulty = "" },
                    rangeYear = new { min = "", max = "" },
                    modifier = ""
                },
                users = new { sortCategory = "postcount" },
                lists = new { sortCategory = "follows" },
                filter = "",
                sort = 0,
                randomizer = 0
            },
            ["useCache"] = false
        };

        // Добавляем динамическое поле, если оно пришло из init
        if (!string.IsNullOrEmpty(hpKey))
        {
            request[hpKey] = hpVal ?? "";
        }

        return request;
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
    /// Выполняет запрос к API HowLongToBeat для получения данных об игре.
    /// </summary>
    /// <param name="searchRequest">Запрос для поиска игры.</param>
    /// <param name="authData">Данные аутентификации.</param>
    /// <returns>Данные о результатах поиска игры.</returns>
    private async Task<HowLongToBeatSearchResponse?> FetchGameDataAsync(object searchRequest, AuthData authData)
    {
        var content = new StringContent(JsonSerializer.Serialize(searchRequest), Encoding.UTF8, "application/json");
        
        using var request = new HttpRequestMessage(HttpMethod.Post, SearchUrl)
        {
            Content = content
        };
        request.Headers.Add("x-auth-token", authData.Token);
        request.Headers.Add("x-hp-key", authData.HpKey);
        request.Headers.Add("x-hp-val", authData.HpVal);

        var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var jsonResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return JsonSerializer.Deserialize<HowLongToBeatSearchResponse>(jsonResponse);
    }

    /// <summary>
    /// Извлекает Token, HpKey и HpVal из API.
    /// </summary>
    private async Task<AuthData?> FetchAuthDataAsync()
    {
        try
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var initUrl = $"{SearchInitUrl}?t={timestamp}";

            var response = await _httpClient.GetAsync(initUrl).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            using var document = JsonDocument.Parse(jsonResponse);
            var root = document.RootElement;

            return new AuthData(
                Token: root.TryGetProperty("token", out var t) ? t.GetString() : null,
                HpKey: root.TryGetProperty("hpKey", out var k) ? k.GetString() : null,
                HpVal: root.TryGetProperty("hpVal", out var v) ? v.GetString() : null
            );
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при получении токена (init)");
            return null;
        }
    }
}