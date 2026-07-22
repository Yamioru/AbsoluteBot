using System.Net;
using System.Text.RegularExpressions;

namespace AbsoluteBot.Services.MediaServices;

#pragma warning disable IDE0028
/// <summary>
///      Сервис для поиска изображений в интернете с использованием Яндекс.Картинки.
/// </summary>
public partial class ImageSearchService(HttpClient httpClient)
{
    private const string UserAgentHeader =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/146.0.0.0 Safari/537.36";

    private const string AcceptLanguageHeader = "ru-RU,ru;q=0.9";
    private const string PlaceholderImageUrl = "https://i.ytimg.com/vi/KoVjqdETurw/maxresdefault.jpg";
    private const int MaxUrlsForSelection = 15;
    private const int MaxCacheSize = 100;
    protected readonly Random Random = new();
    private readonly HashSet<string> _usedImgUrls = new();
    private readonly List<string> _excludeImageWords = new() { "sun9", "shutterstock", "deposit", "alamy" };

    public async Task<string> SearchImageAsync(string text)
    {
        try
        {
            var query = Uri.EscapeDataString(text.Trim());
            var searchUrl = BuildSearchUrl(query);

            var request = new HttpRequestMessage(HttpMethod.Get, searchUrl);
            request.Headers.Add("User-Agent", UserAgentHeader);
            request.Headers.Add("Accept-Language", AcceptLanguageHeader);
            request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");

            var response = await httpClient.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var contentImagesPage = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            var urls = ExtractImageUrls(contentImagesPage);

            return urls.Count == 0 ? PlaceholderImageUrl : SelectRandomUrl(urls);
        }
        catch (Exception)
        {
            return PlaceholderImageUrl;
        }
    }

    private void AddToCache(string url)
    {
        if (_usedImgUrls.Count >= MaxCacheSize)
            _usedImgUrls.Remove(_usedImgUrls.First()); // В HashSet лучше удалять первый, Last() медленнее

        _usedImgUrls.Add(url);
    }

    private static string BuildSearchUrl(string query)
    {
        // Используем параметры из вашего curl: семейный фильтр и свежесть 7 дней (по желанию)
        return $"https://yandex.kz/images/search?text={query}&family=yes";
    }

    private List<string> ExtractImageUrls(string reply)
    {
        // Ищем вхождения img_url=... до кавычки или амперсанда
        var matches = ImageRegex().Matches(reply);
        var urls = new List<string>();

        foreach (Match match in matches)
        {
            // Извлекаем значение из группы (после img_url= или "img_url":")
            string rawUrl = match.Groups[1].Value;

            // 1. Декодируем %3A, %2F и т.д.
            string decodedUrl = WebUtility.UrlDecode(rawUrl);

            // 2. Очищаем от лишних символов (иногда в конце остаются кавычки или слеши)
            decodedUrl = decodedUrl.Trim('"', ' ', '\\');

            // 3. Валидация
            if (Uri.IsWellFormedUriString(decodedUrl, UriKind.Absolute) &&
                !_excludeImageWords.Any(word => decodedUrl.Contains(word, StringComparison.OrdinalIgnoreCase)))
            {
                urls.Add(decodedUrl);
            }
        }

        return urls.Distinct().ToList();
    }

    /// <summary>
    /// Регулярное выражение для поиска прямых ссылок на изображения.
    /// Яндекс часто хранит их в атрибутах data-bem или внутри JSON в формате "img_url":"http..."
    /// </summary>
    [GeneratedRegex(@"(?:img_url(?:""\s*:\s*""|=))([^""&]+)")]
    private static partial Regex ImageRegex();

    private string SelectRandomUrl(List<string> urls)
    {
        var availableUrls = urls.Except(_usedImgUrls).Take(MaxUrlsForSelection).ToList();
        string selectedUrl;

        if (availableUrls.Count == 0)
        {
            selectedUrl = urls[Random.Next(urls.Count)];
        }
        else
        {
            // Берем случайный из доступных, а не всегда первый
            selectedUrl = availableUrls[Random.Next(availableUrls.Count)];
        }

        AddToCache(selectedUrl);
        return selectedUrl;
    }
}