using AbsoluteBot.Chat.Context;
using AbsoluteBot.Events;

namespace AbsoluteBot.Services.ChatServices.Interfaces;

/// <summary>
///     Основной интерфейс чата, предоставляющий возможность подключения и обработки сообщений.
/// </summary>
public interface IChatService
{
    /// <summary>
    ///     Событие, возникающее при получении сообщения.
    /// </summary>
    event EventHandler<MessageReceivedEventArgs> MessageReceived;

    /// <summary>
    ///     Подключает чат-сервис.
    /// </summary>
    Task Connect();

    /// <summary>
    ///     Отправляет сообщение.
    /// </summary>
    /// <param name="message">Текст сообщения.</param>
    /// <param name="context">Контекст чата.</param>
    Task SendMessageAsync(string message, ChatContext context);
}