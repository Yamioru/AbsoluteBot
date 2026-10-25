using AbsoluteBot.Chat.Context;

namespace AbsoluteBot.Services.ChatServices.Interfaces;

/// <summary>
///     Интерфейс для сервисов, которые поддерживают удаление сообщений.
/// </summary>
public interface ISupportsMessageDeletion
{
    /// <summary>
    ///     Удаляет указанное количество последних сообщений в чате.
    /// </summary>
    /// <param name="context">Контекст чата, в котором выполняется удаление.</param>
    /// <param name="count">Количество сообщений для удаления.</param>
    Task DeleteMessagesAsync(ChatContext context, int count);
}