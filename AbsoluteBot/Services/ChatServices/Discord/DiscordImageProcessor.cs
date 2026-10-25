using AbsoluteBot.Chat.Context;
using Discord;
using Serilog;

namespace AbsoluteBot.Services.ChatServices.Discord;

/// <summary>
///     Класс для обработки изображений, полученных из сообщений Discord, с конвертацией их в формат Base64.
/// </summary>
public class DiscordImageProcessor(HttpClient httpClient) : BaseImageProcessor(httpClient)
{
    /// <summary>
    ///     Асинхронно извлекает изображение в формате Base64 из контекста сообщения Discord, если оно существует.
    /// </summary>
    /// <param name="context">Контекст чата Discord.</param>
    /// <returns>Строка в формате Base64, представляющая изображение, или <c>null</c>, если изображение отсутствует.</returns>
    public async Task<string?> GetImageAsBase64Async(DiscordChatContext context)
    {
        try
        {
            var referencedMessage = context.UserMessage;
            if (referencedMessage != null)
            {
                var base64Image = await GetBase64ImageFromMessageAsync(referencedMessage);
                if (base64Image != null) return base64Image;
            }

            var replyMessage = referencedMessage?.ReferencedMessage;
            if (replyMessage != null)
            {
                var base64Image = await GetBase64ImageFromMessageAsync(replyMessage);
                if (base64Image != null) return base64Image;
            }

            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при получении Base64 изображения в Discord.");
            return null;
        }
    }

    /// <summary>
    ///     Асинхронно проверяет наличие изображения в сообщении и конвертирует его в строку Base64.
    /// </summary>
    /// <param name="message">Сообщение пользователя.</param>
    /// <returns>Строка в формате Base64 или <c>null</c>, если изображение отсутствует.</returns>
    private async Task<string?> GetBase64ImageFromMessageAsync(IUserMessage message)
    {
        if (message.Attachments.Count > 0)
            foreach (var attachment in message.Attachments)
                if (attachment.ContentType.StartsWith("image/"))
                    return await DownloadFileFromUrlAsync(attachment.Url);

        var imageUrl = ExtractImageUrlFromText(message.Content);
        if (imageUrl != null) return await DownloadFileFromUrlAsync(imageUrl);

        return null;
    }
}