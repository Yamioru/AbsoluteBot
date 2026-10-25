using System.Net.Http.Headers;
using Newtonsoft.Json;
using AbsoluteBot.Models;
using AbsoluteBot.Services.UtilityServices;
using Serilog;

namespace AbsoluteBot.Services.GoogleSearch;

#pragma warning disable IDE0028
/// <summary>
///     Сервис для выполнения запросов к Google Search API и получения результатов поиска.
/// </summary>
public class GoogleSearchService(HttpClient httpClient, ConfigService configService) : IGoogleSearchService, IAsyncInitializable
{
    private const int DefaultLinkCount = 5;
    private const string CxParameter = "cx";
    private const string QueryParameter = "q";
    private const string ApiKeyParameter = "key";
    private const string SiteSearchFilterParameter = "siteSearchFilter";
    private const string SiteSearchFilterValue = "i";
    private const string SiteSearchParameter = "siteSearch";
    private string? _apiKey;
    private string? _cx;

    public async Task InitializeAsync()
    {
        _apiKey = await configService.GetConfigValueAsync<string>("GoogleSearchApiKey").ConfigureAwait(false);
        _cx = await configService.GetConfigValueAsync<string>("GoogleSearchCXKey").ConfigureAwait(false);
        if (string.IsNullOrEmpty(_apiKey) || string.IsNullOrEmpty(_cx)) Log.Warning("Не удалось загрузить данные для google поиска.");
    }

    /// <summary>
    ///     Выполняет поиск в Google с заданным запросом и возвращает список ссылок на найденные ресурсы.
    /// </summary>
    /// <param name="query">Запрос для поиска.</param>
    /// <param name="linkCount">Количество ссылок, которые должны быть возвращены.</param>
    /// <param name="siteSearchValue">Сайт на котором надо искать</param>
    /// <returns>Список строк с URL-адресами найденных ресурсов или <c>null</c>, если ничего не найдено.</returns>
    public async Task<List<string>?> PerformSearchAsync(string query, int linkCount = DefaultLinkCount, string? siteSearchValue = null)
    {
        try
        {
            var requestUri = BuildRequestUri(query, siteSearchValue);
            if (requestUri == null) return null;
            var response = await SendSearchRequestAsync(requestUri).ConfigureAwait(false);
            return response == null
                ? null
                : ParseSearchResponse(response, linkCount);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при выполнении поиска");
            return null;
        }
    }

    /// <summary>
    ///     Создает строку URI для выполнения поискового запроса в Google.
    /// </summary>
    /// <param name="query">Запрос, который будет выполнен в поисковой системе.</param>
    /// <param name="siteSearchValue">Сайт на котором надо искать</param>
    /// <returns>Строка URI для использования в HTTP-запросе.</returns>
    private string? BuildRequestUri(string query, string? siteSearchValue = null)
    {
        if (string.IsNullOrEmpty(_apiKey) || string.IsNullOrEmpty(_cx)) return null;

        var queryStringParameters = new Dictionary<string, string>
        {
            {CxParameter, _cx},
            {QueryParameter, query},
            {ApiKeyParameter, _apiKey}
        };
        if (siteSearchValue != null)
        {
            queryStringParameters.Add(SiteSearchFilterParameter, SiteSearchFilterValue);
            queryStringParameters.Add(SiteSearchParameter, siteSearchValue);
        }
        var queryString = string.Join("&",
            queryStringParameters.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
        return $"https://customsearch.googleapis.com/customsearch/v1?{queryString}";
    }

    /// <summary>
    ///     Парсит результат поиска Google и извлекает список URL-адресов.
    /// </summary>
    /// <param name="responseBody">Ответ от Google Search API в виде строки JSON.</param>
    /// <param name="linkCount">Максимальное количество ссылок, которые должны быть возвращены.</param>
    /// <returns>
    ///     Список строк с URL-адресами найденных ресурсов. Если ничего не найдено, возвращает <c>null</c>.
    /// </returns>
    private static List<string>? ParseSearchResponse(string responseBody, int linkCount)
    {
        var output = JsonConvert.DeserializeObject<GoogleSearchResponse>(responseBody);
        if (output?.Items.Count is null or 0) return null;

        List<string> result = new();
        var count = 0;

        foreach (var item in output.Items)
        {
            if (!string.IsNullOrEmpty(item.Link))
            {
                result.Add(item.Link);
                count++;
            }

            // Если набрано необходимое количество ссылок - выход
            if (count == linkCount) break;
        }

        return result;
    }

    /// <summary>
    ///     Отправляет HTTP-запрос на Google Search API и возвращает результат.
    /// </summary>
    /// <param name="requestUri">Строка URI для выполнения запроса.</param>
    /// <returns>Строка с JSON-ответом от Google Search API.</returns>
    private async Task<string?> SendSearchRequestAsync(string requestUri)
    {
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri);
        requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await httpClient.SendAsync(requestMessage).ConfigureAwait(false);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadAsStringAsync()
                .ConfigureAwait(false)
            : null;
    }
}