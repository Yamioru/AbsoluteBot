using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Serilog;

namespace AbsoluteBot.Services.UtilityServices;

/// <summary>
///     Сервис для получения основного текстового содержимого веб-страниц с удалением HTML-кода и других элементов.
/// </summary>
public partial class WebContentService
{
    private const string UserAgentString =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/116.0.0.0 Safari/537.36";

    private const string AcceptLanguageHeader = "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7";
    private const int MaxContentLength = 2000;
    private const int MaxDownloadCharacters = 300000;
    private static readonly SemaphoreSlim Semaphore = new(1);
    private readonly HttpClient _httpClient;

    public WebContentService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgentString);
        _httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd(AcceptLanguageHeader);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>
    ///     Получает и извлекает основное текстовое содержимое веб-страниц из списка URL.
    /// </summary>
    /// <param name="urls">Список URL для загрузки контента.</param>
    /// <returns>Список извлечённого текстового содержимого с каждой страницы или <c>null</c> в случае ошибки.</returns>
    public virtual async Task<List<string>?> GetWebContentAsync(List<string> urls)
    {
        var contentList = new List<string>();

        foreach (var url in urls)
            try
            {
                var content = await FetchAndExtractContentAsync(url).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(content)) contentList.Add(content);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при получении содержимого сайта {Url}", url);
            }

        return contentList;
    }

    /// <summary>
    ///     Очищает текст, удаляя HTML-теги, лишние пробелы и ненужные символы.
    /// </summary>
    /// <param name="text">Текст для очистки.</param>
    /// <returns>Очищенный текст без HTML-тегов и лишних пробелов.</returns>
    private static string CleanText(string text)
    {
        text = HtmlTagsRegex().Replace(text, string.Empty);
        text = ExtraSpacesRegex().Replace(text, " ");
        return text.Trim();
    }

    /// <summary>
    ///     Асинхронно загружает содержимое веб-страницы, если её размер не превышает установленный лимит.
    /// </summary>
    /// <param name="url">URL веб-страницы для загрузки.</param>
    /// <returns>Загруженный текстовый контент или null, если загрузка не удалась или превышен лимит символов.</returns>
    private async Task<StringBuilder?> DownloadContentAsync(string url)
    {
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;

        await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var reader = new StreamReader(stream);

        var buffer = new char[1024];
        var content = new StringBuilder();
        var totalCharsRead = 0;

        while (!reader.EndOfStream)
        {
            var charsRead = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
            totalCharsRead += charsRead;

            // Прерывание, если загружено больше 300000 символов
            if (totalCharsRead > MaxDownloadCharacters) return null;

            content.Append(buffer, 0, charsRead);
        }

        return content;
    }

    /// <summary>
    ///     Извлекает текст из определённых тегов HTML-документа.
    /// </summary>
    /// <param name="content">HTML-контент для извлечения текста.</param>
    /// <returns>Текст, извлечённый из тегов, с ограничением на количество символов.</returns>
    private static string ExtractRelevantText(StringBuilder content)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(content.ToString());

        var extractedContent = new StringBuilder();
        var nodesToExtract = doc.DocumentNode.SelectNodes("//p | //h1 | //h2 | //h3 | //article | //section | //blockquote");

        if (nodesToExtract == null) return string.Empty;

        foreach (var node in nodesToExtract)
        {
            var cleanedText = CleanText(node.InnerText);

            // Ограничение длины итогового текста
            if (extractedContent.Length + cleanedText.Length > MaxContentLength)
            {
                var remainingLength = MaxContentLength - extractedContent.Length;
                extractedContent.AppendLine(cleanedText[..remainingLength]);
                break;
            }

            if (!string.IsNullOrWhiteSpace(cleanedText)) extractedContent.AppendLine(cleanedText);
        }

        return extractedContent.ToString().Trim();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex ExtraSpacesRegex();

    /// <summary>
    ///     Асинхронно загружает и извлекает содержимое веб-страницы по указанному URL.
    /// </summary>
    /// <param name="url">URL веб-страницы для извлечения содержимого.</param>
    /// <returns>
    ///     Извлечённый текст с ограничением по количеству символов или null, если произошла ошибка или размер слишком
    ///     большой.
    /// </returns>
    private async Task<string?> FetchAndExtractContentAsync(string url)
    {
        await Semaphore.WaitAsync(); // Захват семафора для ограничения одновременных вызовов
        try
        {
            // Попытка получить заголовки и проверить размер страницы
            if (await IsContentTooLargeAsync(url)) return null;

            // Загрузка и чтение содержимого страницы
            var content = await DownloadContentAsync(url);
            if (content == null) return null;

            // Извлечение текстового содержимого из тегов
            return ExtractRelevantText(content);
        }
        finally
        {
            Semaphore.Release(); // Освобождение семафора после завершения работы метода
        }
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTagsRegex();

    /// <summary>
    ///     Проверяет, является ли содержимое веб-страницы слишком большим (более 300000 символов).
    /// </summary>
    /// <param name="url">URL веб-страницы для проверки.</param>
    /// <returns>True, если контент слишком большой, иначе False.</returns>
    private async Task<bool> IsContentTooLargeAsync(string url)
    {
        var headRequest = new HttpRequestMessage(HttpMethod.Head, url);
        var headResponse = await _httpClient.SendAsync(headRequest).ConfigureAwait(false);

        if (!headResponse.IsSuccessStatusCode) return true;

        return headResponse.Content.Headers.ContentLength is > MaxDownloadCharacters;
    }
}