using System.Text.RegularExpressions;
using Serilog;

namespace AbsoluteBot.Services.MediaServices;
#pragma warning disable IDE0028
/// <summary>
///     Сервис для поиска видео на YouTube по заданному тексту.
/// </summary>
public partial class VideoSearchService(HttpClient httpClient)
{
    private const string DefaultVideoUrl = "https://youtu.be/dQw4w9WgXcQ";
    private const string YouTubeSearchUrl = "https://www.youtube.com/results?search_query=";
    private const int MaxCacheSize = 100;
    private const int MaxLoadVideos = 3;
    protected readonly Random Random = new();
    private readonly HashSet<string> _sentVideos = new();

    /// <summary>
    ///     Выполняет асинхронный поиск видео на YouTube по заданному тексту.
    /// </summary>
    /// <param name="text">Текст для поиска видео.</param>
    /// <returns>URL найденного видео или URL по умолчанию, если поиск не удался.</returns>
    public virtual async Task<string> SearchVideoAsync(string text)
    {
        try
        {
            var word = PrepareSearchQuery(text.ToLower());
            var url = BuildSearchUrl(word);

            var reply = await FetchSearchResultsAsync(url).ConfigureAwait(false);

            var videoIds = ExtractVideoIds(reply);
            if (videoIds.Count == 0) return DefaultVideoUrl;

            var videoId = GetUniqueVideoId(videoIds);
            return $"https://youtu.be/{videoId}";
        }
        catch (HttpRequestException ex)
        {
            Log.Error(ex, "Ошибка при поиске видео - HTTP запрос не выполнен.");
            return DefaultVideoUrl;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Непредвиденная ошибка при поиске видео.");
            return DefaultVideoUrl;
        }
    }

    /// <summary>
    ///     Добавляет URL в кэш, удаляя старые URL, если размер кэша превышает ограничение.
    /// </summary>
    /// <param name="url">URL для добавления в кэш.</param>
    private void AddToCache(string url)
    {
        if (_sentVideos.Count >= MaxCacheSize)
            // Удаление старейшего элемента из кэша
            _sentVideos.Remove(_sentVideos.Last());

        _sentVideos.Add(url);
    }

    /// <summary>
    ///     Строит URL для поиска видео на YouTube по заданному поисковому запросу.
    /// </summary>
    /// <param name="query">Поисковый запрос.</param>
    /// <returns>URL для поиска на YouTube.</returns>
    private static string BuildSearchUrl(string query)
    {
        return string.Join(string.Empty, YouTubeSearchUrl, query);
    }

    /// <summary>
    ///     Извлекает уникальные идентификаторы видео из HTML-кода страницы поиска YouTube.
    /// </summary>
    /// <param name="html">HTML-код страницы поиска.</param>
    /// <returns>Список идентификаторов видео.</returns>
    private static List<string> ExtractVideoIds(string html)
    {
        var videoIds = new HashSet<string>();

        // Поиск всех совпадений
        var matches = VideoRegex().Matches(html);

        foreach (Match match in matches)
        {
            var videoId = match.Groups[1].Value;
            videoIds.Add(videoId);

            // Ограничение на количество найденных видео
            if (videoIds.Count >= MaxLoadVideos) break;
        }

        return videoIds.ToList();
    }

    /// <summary>
    ///     Выполняет HTTP-запрос для получения результатов поиска на YouTube.
    /// </summary>
    /// <param name="url">URL для выполнения запроса.</param>
    /// <returns>HTML-код страницы с результатами поиска.</returns>
    /// <exception cref="HttpRequestException">Возникает, если HTTP-запрос не выполнен успешно.</exception>
    private async Task<string> FetchSearchResultsAsync(string url)
    {
        var response = await httpClient.GetAsync(url).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     Возвращает случайный идентификатор видео из списка найденных.
    /// </summary>
    /// <param name="videoIds">Список идентификаторов видео.</param>
    /// <returns>Случайный идентификатор видео.</returns>
    private string GetRandomVideoId(List<string> videoIds)
    {
        var randomVideoId = videoIds[Random.Next(videoIds.Count)];
        AddToCache(randomVideoId);
        return randomVideoId;
    }

    /// <summary>
    ///     Возвращает уникальный идентификатор видео, который еще не был отправлен.
    /// </summary>
    /// <param name="videoIds">Список идентификаторов видео.</param>
    /// <returns>Уникальный идентификатор видео.</returns>
    private string GetUniqueVideoId(List<string> videoIds)
    {
        var filteredVideoIds = videoIds.Except(_sentVideos).ToList();

        if (filteredVideoIds.Count <= 0) return GetRandomVideoId(videoIds);
        var selectedVideoId = filteredVideoIds.First();
        _sentVideos.Add(selectedVideoId);
        return selectedVideoId;
    }

    /// <summary>
    ///     Очищает текст команды, заменяя пробелы на "+" для использования в URL поискового запроса.
    /// </summary>
    /// <param name="text">Текст команды для очистки.</param>
    /// <returns>Очищенный текст, пригодный для использования в URL.</returns>
    private static string PrepareSearchQuery(string text)
    {
        return text.Trim().Replace(" ", "+");
    }

    [GeneratedRegex("\"videoId\":\"([a-zA-Z0-9_-]{11})\"")]
    private static partial Regex VideoRegex();
}