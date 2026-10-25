using System.Text.RegularExpressions;
using Serilog;

namespace AbsoluteBot.Services.MediaServices;

#pragma warning disable IDE0028

/// <summary>
///     Сервис для поиска GIF изображений через Google и возврата URL найденных GIF.
/// </summary>
public partial class GifSearchService
{
    private const string PlaceholderGifUrl = "https://media.tenor.com/8WQ2kuLp2MUAAAAM/shlepa.gif";
    private const string GoogleImageSearchUrl = "https://www.google.com/search?q={0}&tbm=isch&safe=active";
    private const string AcceptLanguageHeader = "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7";
    private const string UserAgentString = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/116.0.0.0 Safari/537.36";
    private const string VkImagePrefix = "sun9";
    private const int MaxCacheSize = 100;
    protected readonly Random Random = new();
    private readonly HashSet<string> _usedGifUrls = new();
    private readonly HttpClient _httpClient;

    public GifSearchService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgentString);
        _httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd(AcceptLanguageHeader);
    }

    /// <summary>
    ///     Выполняет асинхронный поиск GIF на основе заданного текста запроса.
    /// </summary>
    /// <param name="text">Текст запроса для поиска GIF.</param>
    /// <returns>URL найденной GIF или URL изображения котика, если ничего не найдено.</returns>
    public virtual async Task<string> SearchGifAsync(string text)
    {
        try
        {
            var query = CleanQuery(text);
            var searchUrl = BuildSearchUrl(query);

            using var response = await _httpClient.GetAsync(searchUrl).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var googleImagePageContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!googleImagePageContent.Contains(".gif\""))
                return PlaceholderGifUrl;

            var gifUrls = ExtractImageUrls(googleImagePageContent);

            //Возвращается случайная гифка из найденных или если ничего не найдено котик
            return gifUrls.Count == 0 ? PlaceholderGifUrl : await SelectRandomUrl(gifUrls);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при поиске гифки.");
            return PlaceholderGifUrl;
        }
    }

    /// <summary>
    ///     Добавляет URL в кэш, удаляя старые URL, если размер кэша превышает ограничение.
    /// </summary>
    /// <param name="url">URL для добавления в кэш.</param>
    private void AddToCache(string url)
    {
        if (_usedGifUrls.Count >= MaxCacheSize)
            // Удаление старейшего элемента из кэша
            _usedGifUrls.Remove(_usedGifUrls.Last());

        _usedGifUrls.Add(url);
    }

    /// <summary>
    ///     Формирует URL для поиска изображений в Google на основе заданного запроса
    /// </summary>
    /// <param name="query">Запрос для поиска.</param>
    /// <returns>Сформированный URL для поиска в Google.</returns>
    private static string BuildSearchUrl(string query)
    {
        query = ".gif+" + query;
        return string.Format(GoogleImageSearchUrl, query);
    }

    /// <summary>
    ///     Очищает и форматирует текстовый запрос для поиска.
    /// </summary>
    /// <param name="text">Текст запроса.</param>
    /// <returns>Очищенный и отформатированный запрос.</returns>
    private static string CleanQuery(string text)
    {
        text = text.Trim().Replace(" ", "+");
        return text.ToLower();
    }

    /// <summary>
    ///     Извлекает URL изображений из содержимого страницы поиска Google.
    /// </summary>
    /// <param name="googleImagePageContent">Содержимое страницы поиска Google.</param>
    /// <returns>Список извлеченных URL изображений.</returns>
    private static List<string> ExtractImageUrls(string googleImagePageContent)
    {
        var matchList = GifRegex().Matches(googleImagePageContent);
        var urls = matchList.Select(match => match.Value.TrimEnd('"')).ToList();

        // Удаление url изображений из вконтакте
        urls.RemoveAll(url => url.Contains(VkImagePrefix));

        return urls;
    }

    [GeneratedRegex("http[:a-zA-Z.\\/0-9_-]*gif\"")]
    private static partial Regex GifRegex();

    /// <summary>
    ///     Проверка url ссылки на то gif это или нет.
    /// </summary>
    /// <param name="url">Url гифки.</param>
    /// <returns>True если это gif, иначе false.</returns>
    private async Task<bool> IsValidGifUrlAsync(string url)
    {
        try
        {
            // Выполнение запроса на заголовки (метод HEAD)
            var response = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
            response.EnsureSuccessStatusCode();

            // Проверка Content-Type
            var contentType = response.Content.Headers.ContentType?.MediaType;
            return contentType != null && contentType.Equals("image/gif", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Ошибка проверки URL: {url}");
            return false;
        }
    }

    /// <summary>
    ///     Выбирает случайный URL из списка URL, избегая повторного использования недавно выбранных URL.
    /// </summary>
    /// <param name="urls">Список доступных URL для выбора.</param>
    /// <returns>Выбранный случайный URL.</returns>
    private async Task<string> SelectRandomUrl(List<string> urls)
    {
        // Фильтруется список URL, удаляя те, которые уже были использованы
        var availableUrls = urls.Except(_usedGifUrls).Take(15).ToList();
        // Если после фильтрации список стал пустым, используется оригинальный список
        if (availableUrls.Count == 0)
        {
            availableUrls = urls;
            var selectedUrl = availableUrls[Random.Next(availableUrls.Count)];
            AddToCache(selectedUrl);
            return selectedUrl;
        }

        foreach (var url in availableUrls)
        {
            if (!await IsValidGifUrlAsync(url)) continue;
            AddToCache(url);
            return url;
        }


        return PlaceholderGifUrl;
    }
}