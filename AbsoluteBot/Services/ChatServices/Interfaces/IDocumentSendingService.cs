using AbsoluteBot.Chat.Context;

namespace AbsoluteBot.Services.ChatServices.Interfaces;

/// <summary>
///     Интерфейс для чат-сервисов, которые поддерживают отправку документов.
/// </summary>
public interface IDocumentSendingService
{
    /// <summary>
    ///     Отправляет документ.
    /// </summary>
    /// <param name="message">Документ для отправки.</param>
    /// <param name="context">Контекст чата.</param>
    Task SendDocumentAsync(string message, ChatContext context);
}