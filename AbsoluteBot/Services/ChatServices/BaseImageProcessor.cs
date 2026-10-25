using System.Text.RegularExpressions;
using Serilog;

namespace AbsoluteBot.Services.ChatServices;

/// <summary>
///     Базовый класс для процессоров изображений, предоставляющий общую логику для загрузки изображений и их
///     конвертации в формат Base64.
/// </summary>
public abstract partial class BaseImageProcessor(HttpClient httpClient)
{
    /// <summary>
    ///     Асинхронно загружает файл по URL и возвращает его содержимое в формате Base64, если это изображение.
    /// </summary>
    /// <param name="url">URL файла.</param>
    /// <returns>Строка в формате Base64 или <c>null</c>, если файл не является изображением или произошла ошибка.</returns>
    protected async Task<string?> DownloadFileFromUrlAsync(string url)
    {
        try
        {
            // Проверка MIME-типа перед загрузкой файла
            var mimeType = await GetMimeTypeFromUrlAsync(url);
            if (mimeType != null && mimeType.StartsWith("image/"))
            {
                var fileBytes = await httpClient.GetByteArrayAsync(url).ConfigureAwait(false);
                return Convert.ToBase64String(fileBytes);
            }

            Log.Warning($"Файл по URL не является изображением: {url}, MIME-тип: {mimeType}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Ошибка при загрузке файла из URL: {url}");
        }

        return null;
    }

    /// <summary>
    ///     Извлекает URL изображения из текста сообщения, если он существует.
    /// </summary>
    /// <param name="messageText">Текст сообщения.</param>
    /// <returns>URL изображения или <c>null</c>, если не найдено.</returns>
    protected static string? ExtractImageUrlFromText(string messageText)
    {
        var match = UrlRegex().Match(messageText);
        return match.Success ? match.Value : null;
    }

    /// <summary>
    ///     Проверяет, является ли URL ссылкой на изображение.
    /// </summary>
    /// <param name="url">URL для проверки.</param>
    /// <returns>True, если URL является ссылкой на изображение, иначе False.</returns>
    protected static bool IsImageUrl(string url)
    {
        return UrlRegex().IsMatch(url);
    }

    /// <summary>
    ///     Асинхронно получает MIME-тип содержимого по URL.
    /// </summary>
    /// <param name="url">URL файла.</param>
    /// <returns>MIME-тип или <c>null</c>, если тип не может быть определен.</returns>
    private async Task<string?> GetMimeTypeFromUrlAsync(string url)
    {
        try
        {
            // Выполнение HEAD-запроса для получения заголовков, включая Content-Type
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);

            if (response is {IsSuccessStatusCode: true, Content.Headers.ContentType: not null}) return response.Content.Headers.ContentType.MediaType;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Ошибка при получении MIME-типа по URL: {url}");
        }

        return null;
    }

    [GeneratedRegex(@"(http(s?):)([/|.|\w|\s|-])*", RegexOptions.IgnoreCase)]
    private static partial Regex UrlRegex();
}