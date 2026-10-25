using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;

namespace AbsoluteBot.Messaging;

/// <summary>
///     Интерфейс для парсинга команд, отправленных в чат.
/// </summary>
public interface ICommandParser
{
    /// <summary>
    ///     Парсит сообщение чата для извлечения команды и её параметров.
    /// </summary>
    /// <param name="message">Сообщение, отправленное в чат.</param>
    /// <param name="context">Контекст чата, в котором было отправлено сообщение.</param>
    /// <param name="userRole">Роль пользователя, отправившего сообщение.</param>
    /// <returns>
    ///     Объект <see cref="ParsedCommand" />, содержащий команду и её параметры, или <c>null</c>, если команда не
    ///     распознана.
    /// </returns>
    ParsedCommand? Parse(string message, ChatContext context, UserRole userRole);
}