using AbsoluteBot.Chat.Context;
using Serilog;

namespace AbsoluteBot.Services.ChatServices.TwitchChat;

/// <summary>
///     Класс для обработки ссылок на изображения в сообщениях Twitch и конвертации их в формат Base64.
/// </summary>
public class TwitchImageProcessor(HttpClient httpClient) : BaseImageProcessor(httpClient)
{
    /// <summary>
    ///     Проверяет сообщение на наличие ссылок на изображения и возвращает содержимое изображения в формате Base64.
    /// </summary>
    /// <param name="message">Текст сообщения для проверки.</param>
    /// <param name="context">Контекст сообщения для проверки.</param>
    /// <returns>Строка в формате Base64 или <c>null</c>, если изображение не найдено.</returns>
    public async Task<string?> GetBase64ImageFromMessageAsync(string message, ChatContext context)
    {
        try
        {
            var base64Image = await CheckMessageForImage(message).ConfigureAwait(false);
            if (base64Image != null) return base64Image;

            if (context.Reply?.Message == null) return null;
            base64Image = await CheckMessageForImage(context.Reply.Message).ConfigureAwait(false);
            return base64Image ?? null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при проверке сообщения на наличие изображения в Twitch.");
            return null;
        }
    }

    /// <summary>
    ///     Проверяет текст сообщения на наличие ссылки на изображение.
    /// </summary>
    /// <param name="messageText">Текст сообщения.</param>
    /// <returns>Строка в формате Base64 или <c>null</c>, если изображение не найдено.</returns>
    private async Task<string?> CheckMessageForImage(string? messageText)
    {
        if (string.IsNullOrWhiteSpace(messageText)) return null;

        var imageUrl = ExtractImageUrlFromText(messageText);
        if (imageUrl != null) return await DownloadFileFromUrlAsync(imageUrl).ConfigureAwait(false);

        return null;
    }
}