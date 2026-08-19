using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using AbsoluteBot.Services.GoogleSearch;
using AbsoluteBot.Services.UtilityServices;
using HtmlAgilityPack;
using Serilog;

namespace AbsoluteBot.Services.UserManagementServices;

/// <summary>
/// Сервис для работы с MBTI типами пользователей и получения данных о персонажах по MBTI.
/// </summary>
public partial class MbtiService(HttpClient httpClient, GoogleSearchService googleSearchService) : IAsyncInitializable
{
    private static string FilePath => DataPaths.Get("mbti_data.json");

    private const string UserAgentString =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/116.0.0.0 Safari/537.36";

    private const string AcceptLanguageHeader = "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7";
    private const string GoogleSearchUrlTemplate = "https://www.google.com/search?safe=active&q={0}+personality-database.com+\"sub_cat_id\"";
    private const int DefaultMbtiNumber = 1;
    private const string PersonalityDatabaseUrl = "https://www.personality-database.com/";

    private const string ApiRequestUrlTemplate =
        "https://api.personality-database.com/api/v1/profiles?limit=1&sub_cat_id={0}&cat_id={1}&property_id={2}&type={3}";

    private static readonly SemaphoreSlim Semaphore = new(1, 1);

    private static readonly Dictionary<string, int> MbtiMapping = new()
    {
        {"ISTJ", 1},
        {"ESTJ", 2},
        {"ISFJ", 3},
        {"ESFJ", 4},
        {"ESFP", 5},
        {"ISFP", 6},
        {"ESTP", 7},
        {"ISTP", 8},
        {"INFJ", 9},
        {"ENFJ", 10},
        {"INFP", 11},
        {"ENFP", 12},
        {"INTP", 13},
        {"ENTP", 14},
        {"INTJ", 15},
        {"ENTJ", 16}
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private ConcurrentDictionary<string, string> _mbtiData = new();

    public async Task InitializeAsync()
    {
        try
        {
            await LoadMbtiAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при инициализации MbtiService.");
            throw;
        }
    }

    /// <summary>
    /// Получает персонажа, соответствующего указанному MBTI и игре.
    /// </summary>
    /// <param name="game">Название игры.</param>
    /// <param name="mbti">Тип MBTI.</param>
    /// <returns>Имя персонажа, если найдено.</returns>
    public async Task<string?> GetCharacterByMbtiAsync(string game, string mbti)
    {
        try
        {
            var character = await FetchCharacterFromApiAsync(game, mbti).ConfigureAwait(false);
            return character;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Произошла ошибка при попытке получить данные.");
            return null;
        }
    }

    /// <summary>
    /// Возвращает MBTI пользователя по его имени.
    /// </summary>
    /// <param name="username">Имя пользователя.</param>
    /// <returns>Тип MBTI, если найден.</returns>
    public virtual string? GetMbtiForUser(string username)
    {
        try
        {
            return _mbtiData.TryGetValue(username.ToLower(), out var mbti) ? mbti : null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при получении mbti пользователя.");
            return null;
        }
    }

    /// <summary>
    /// Проверяет, является ли переданный MBTI действительным типом.
    /// </summary>
    /// <param name="mbti">Тип MBTI для проверки.</param>
    /// <returns><c>true</c>, если MBTI действителен, иначе <c>false</c>.</returns>
    public static bool IsValidMbti(string mbti)
    {
        var normalizedMbti = mbti.Trim().ToUpper();
        return MbtiMapping.ContainsKey(normalizedMbti);
    }

    /// <summary>
    /// Преобразует MBTI тип в числовое значение.
    /// </summary>
    /// <param name="mbti">Тип MBTI.</param>
    /// <returns>Числовое значение, соответствующее типу MBTI.</returns>
    public static int MbtiToNumber(string mbti)
    {
        var normalizedMbti = mbti.Trim().ToUpper();
        return MbtiMapping.TryGetValue(normalizedMbti, out var number) ? number : DefaultMbtiNumber;
    }

    /// <summary>
    /// Устанавливает тип MBTI для пользователя и сохраняет данные.
    /// </summary>
    /// <param name="username">Имя пользователя.</param>
    /// <param name="mbti">Тип MBTI.</param>
    /// <returns><c>true</c>, если операция успешна, иначе <c>false</c>.</returns>
    public virtual async Task<bool> SetMbtiForUserAsync(string username, string mbti)
    {
        try
        {
            _mbtiData[username.ToLower()] = mbti.ToUpper();
            await SaveDataAsync().ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при добавлении MBTI для пользователя.");
            return false;
        }
    }

    [GeneratedRegex(@"\d+")]
    private static partial Regex DigitsRegex();

    /// <summary>
    /// Извлекается первая ссылка на сайт Personality Database из результатов поиска.
    /// </summary>
    /// <param name="responseBody">Тело ответа с результатами поиска.</param>
    /// <param name="targetUrl">Целевая ссылка на сайт для поиска.</param>
    /// <returns>Найденная ссылка или null, если ссылка не найдена.</returns>
    private static string? ExtractMatchingLink(string responseBody, string targetUrl)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(responseBody);

        var links = doc.DocumentNode.SelectNodes("//a[@href]");
        return links?.Select(link => link.GetAttributeValue("href", string.Empty))
            .FirstOrDefault(href => href.StartsWith(targetUrl));
    }

    /// <summary>
    /// Выполняется запрос к API Personality Database для получения данных о персонаже.
    /// </summary>
    /// <param name="apiRequestUrl">URL для запроса к API.</param>
    /// <returns>Информация о персонаже в виде строки или null, если персонаж не найден.</returns>
    private async Task<string?> FetchCharacterDataFromApiAsync(string apiRequestUrl)
    {
        const string mbtiProfileString = "\"mbti_profile\"";
        var apiRequest = new HttpRequestMessage(HttpMethod.Get, apiRequestUrl);
        var apiResponse = await httpClient.SendAsync(apiRequest).ConfigureAwait(false);
        apiResponse.EnsureSuccessStatusCode();

        var apiResponseBody = await apiResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
        var startIdx = apiResponseBody.IndexOf(mbtiProfileString, StringComparison.Ordinal);

        if (startIdx <= 0)
        {
            Log.Warning("startIdx is null" + apiRequestUrl);
            return null;
        }

        var responseContent = apiResponseBody[(startIdx + mbtiProfileString.Length)..];
        var startIdx2 = responseContent.IndexOf('\"', StringComparison.Ordinal);
        responseContent = responseContent[(startIdx2 + 1)..];
        var endIdx = responseContent.IndexOf('\"', StringComparison.Ordinal);

        return responseContent[..endIdx];
    }

    /// <summary>
    /// Асинхронный метод для получения персонажа по MBTI из базы данных Personality Database.
    /// Поиск осуществляется через Google с последующей обработкой результата для получения данных.
    /// </summary>
    /// <param name="game">Название игры для поиска персонажей.</param>
    /// <param name="mbti">Тип MBTI для персонажа.</param>
    /// <returns>Строка с информацией о персонаже или null, если ничего не найдено.</returns>
    private async Task<string?> FetchCharacterFromApiAsync(string game, string mbti)
    {
        // Отправляется HTTP-запрос для получения страницы с поисковыми результатами
        var links = await googleSearchService.PerformSearchAsync(game + " " + "\"sub_cat_id\"", 10, "https://www.personality-database.com/");
        if (links == null || links.Count < 1)
        {
            Log.Warning("links is empty");
            return null;
        }
        File.WriteAllLines(DataPaths.Get("mbtipath.txt"), links);
        var matchingLink = links.FirstOrDefault(link => link.Contains(PersonalityDatabaseUrl));
        // Извлекается ссылка на сайт Personality Database
        if (matchingLink == null)
        {
            Log.Warning("matchingLink is null");
            return null;
        }

        // Генерация URL для API-запроса на основе извлеченной ссылки
        var apiRequestUrl = GenerateApiRequestUrl(matchingLink, mbti);
        if (apiRequestUrl == null)
        {
            Log.Warning("apiRequestUrl is null " + matchingLink);
            return null;
        }

        // Выполняется запрос к API для получения данных о персонаже
        return await FetchCharacterDataFromApiAsync(apiRequestUrl).ConfigureAwait(false);
    }

    /// <summary>
    /// Выполняется асинхронный HTTP-запрос к Google для получения результатов поиска.
    /// </summary>
    /// <param name="url">URL для запроса в Google.</param>
    /// <returns>Тело ответа в виде строки или null, если запрос не удался.</returns>
    private async Task<string?> FetchGoogleSearchResultsAsync(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("User-Agent", UserAgentString);
        request.Headers.Add("accept-language", AcceptLanguageHeader);

        var response = await httpClient.SendAsync(request).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;

        return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Генерируется URL для запроса к API на основе извлеченной ссылки и MBTI.
    /// </summary>
    /// <param name="matchingLink">Ссылка на страницу с персонажем.</param>
    /// <param name="mbti">Тип MBTI для поиска.</param>
    /// <returns>URL для API-запроса или null, если не удается извлечь данные для запроса.</returns>
    private static string? GenerateApiRequestUrl(string matchingLink, string mbti)
    {
        var sanitizedLink = matchingLink.Replace("amp;", "");
        var regex = DigitsRegex();
        var matches = regex.Matches(sanitizedLink).Select(m => m.Value).ToList();

        return matches.Count < 3
            ? null
            : string.Format(ApiRequestUrlTemplate, matches[2], matches[1], matches[0], MbtiToNumber(mbti));
    }

    /// <summary>
    /// Загружает данные Mbti из файла или создаёт.
    /// </summary>
    /// <returns></returns>
    private async Task LoadMbtiAsync()
    {
        await Semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (File.Exists(FilePath))
            {
                var json = await File.ReadAllTextAsync(FilePath).ConfigureAwait(false);
                _mbtiData = JsonSerializer.Deserialize<ConcurrentDictionary<string, string>>(json) ??
                            new ConcurrentDictionary<string, string>();
            }
            else
            {
                _mbtiData = new ConcurrentDictionary<string, string>();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при загрузке MBTI данных.");
        }
        finally
        {
            Semaphore.Release(); // Освобождение семафора
        }
    }

    /// <summary>
    /// Сохраняет данные MBTI в файл.
    /// </summary>
    private async Task SaveDataAsync()
    {
        await Semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            var json = JsonSerializer.Serialize(_mbtiData, JsonOptions);
            await File.WriteAllTextAsync(FilePath, json).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при сохранении MBTI данных.");
        }
        finally
        {
            Semaphore.Release(); // Освобождение семафора
        }
    }
}