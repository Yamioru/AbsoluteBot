using AbsoluteBot.Chat.Context;

namespace AbsoluteBot.Services.ChatServices.Interfaces;

/// <summary>
/// Интерфейс для чат-сервисов, которые поддерживают отправку фотографий.
/// </summary>
public interface IPhotoSendingService
{
    /// <summary>
    /// Отправляет фотографию.
    /// </summary>
    /// <param name="url">URL фотографии.</param>
    /// <param name="context">Контекст чата.</param>
    Task SendPhotoAsync(string url, ChatContext context);

    /// <summary>
    /// Отправляет фотографию.
    /// </summary>
    /// <param name="base64Image">base 64 строка изображения.</param>
    /// <param name="context">Контекст чата.</param>
    Task SendPhotoBase64Async(string base64Image, ChatContext context);
}