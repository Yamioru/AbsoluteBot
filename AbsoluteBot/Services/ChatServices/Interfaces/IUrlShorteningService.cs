using AbsoluteBot.Chat.Context;

namespace AbsoluteBot.Services.ChatServices.Interfaces;

/// <summary>
///     Интерфейс для чат-сервисов, которые поддерживают отправку URL-ов с сокращением ссылок.
/// </summary>
public interface IUrlShorteningService
{
    /// <summary>
    ///     Отправляет сокращенную ссылку.
    /// </summary>
    /// <param name="url">URL для сокращения и отправки.</param>
    /// <param name="context">Контекст чата.</param>
    Task SendShortenedUrlAsync(string url, ChatContext context);
}