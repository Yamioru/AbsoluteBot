using AbsoluteBot.Chat.Context;
using Serilog;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace AbsoluteBot.Services.ChatServices.TelegramChat;

/// <summary>
///     Класс для обработки файлов и ссылок на изображения из Telegram и их конвертации в формат Base64.
/// </summary>
public class TelegramImageProcessor(HttpClient httpClient) : BaseImageProcessor(httpClient)
{
    /// <summary>
    ///     Асинхронно проверяет сообщение на наличие файла или ссылки на изображение и возвращает его в формате Base64.
    /// </summary>
    /// <param name="botClient">Клиент Telegram.</param>
    /// <param name="telegramChatContext">Контекст чата телеграм.</param>
    /// <returns>Строка в формате Base64 или <c>null</c>, если файл или ссылка на изображение не найдены.</returns>
    public async Task<string?> GetBase64FileOrImageFromMessageAsync(ITelegramBotClient botClient, TelegramChatContext telegramChatContext)
    {
        try
        {
            // Проверка текущего сообщения пользователя на наличие файла или ссылки на изображение
            var base64Image = await CheckMessageForFileOrImage(botClient, telegramChatContext.Message).ConfigureAwait(false);
            if (base64Image != null) return base64Image;

            // Проверка ответного сообщения, если оно существует
            if (telegramChatContext.Message.ReplyToMessage == null) return null;
            base64Image = await CheckMessageForFileOrImage(botClient, telegramChatContext.Message.ReplyToMessage).ConfigureAwait(false);
            return base64Image ?? null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при получении Base64 изображения в Telegram.");
            return null;
        }
    }

    /// <summary>
    ///     Асинхронно проверяет сообщение на наличие файла или ссылки на изображение и возвращает его в формате Base64.
    /// </summary>
    /// <param name="botClient">Клиент Telegram.</param>
    /// <param name="message">Сообщение, которое нужно проверить.</param>
    /// <returns>Строка в формате Base64 или <c>null</c>, если файл или ссылка на изображение не найдены.</returns>
    private async Task<string?> CheckMessageForFileOrImage(ITelegramBotClient botClient, Message message)
    {
        if (message.Photo is {Length: > 0})
        {
            var fileId = message.Photo[^1].FileId;
            return await GetFileBase64Async(botClient, fileId).ConfigureAwait(false);
        }

        if (message.Text == null) return null;
        var imageUrl = ExtractImageUrlFromText(message.Text);
        if (imageUrl != null) return await DownloadFileFromUrlAsync(imageUrl).ConfigureAwait(false);

        return null;
    }

    /// <summary>
    ///     Асинхронно конвертирует файл Telegram в строку Base64 по его идентификатору.
    /// </summary>
    /// <param name="fileId">Идентификатор файла.</param>
    /// <param name="botClient">Клиент Telegram.</param>
    /// <returns>Строка в формате Base64 или <c>null</c>, если произошла ошибка.</returns>
    private static async Task<string?> GetFileBase64Async(ITelegramBotClient botClient, string fileId)
    {
        try
        {
            var file = await botClient.GetFileAsync(fileId).ConfigureAwait(false);
            if (file.FilePath == null) return null;
            using var stream = new MemoryStream();
            await botClient.DownloadFile(file.FilePath, stream).ConfigureAwait(false);
            var fileBytes = stream.ToArray();
            return Convert.ToBase64String(fileBytes);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Ошибка при получении файла по идентификатору {fileId} из Telegram.");
            return null;
        }
    }
}