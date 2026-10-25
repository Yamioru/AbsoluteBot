using System.Text.RegularExpressions;
using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;

namespace AbsoluteBot.Messaging;

/// <summary>
///     Класс для парсинга команд, отправленных в чат. Реализует интерфейс <see cref="ICommandParser" />.
/// </summary>
public partial class CommandParser : ICommandParser
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
    public ParsedCommand? Parse(string message, ChatContext context, UserRole userRole)
    {
        // Проверяется валидность команды
        var command = ExtractCommand(message);
        if (string.IsNullOrEmpty(command)) return null;

        // Извлекаются параметры команды
        var parameters = ExtractParameters(message, command.Length);

        return CreateParsedCommand(command, parameters, context, userRole);
    }

    /// <summary>
    ///     Универсальное регулярное выражение для поиска команды, начинающейся с ! или @
    /// </summary>
    [GeneratedRegex(@"^[!@]\s*([^\s]+)", RegexOptions.IgnoreCase, "ru-RU")]
    private static partial Regex CommandRegex();

    /// <summary>
    ///     Создает объект ParsedCommand из команды, параметров и контекста.
    /// </summary>
    /// <param name="command">Команда, извлеченная из сообщения.</param>
    /// <param name="parameters">Параметры команды.</param>
    /// <param name="context">Контекст чата.</param>
    /// <param name="userRole">Роль пользователя.</param>
    /// <returns>Объект ParsedCommand.</returns>
    private static ParsedCommand CreateParsedCommand(string command, string parameters, ChatContext context,
        UserRole userRole)
    {
        return new ParsedCommand(command, parameters, context, userRole);
    }

    /// <summary>
    ///     Извлекает команду из сообщения с помощью регулярного выражения.
    /// </summary>
    /// <param name="message">Сообщение из чата.</param>
    /// <returns>Извлеченная команда или null, если команда не найдена.</returns>
    private static string? ExtractCommand(string message)
    {
        var match = CommandRegex().Match(message);
        return match.Success ? match.Groups[0].Value.Trim().ToLowerInvariant() : null;
    }

    /// <summary>
    ///     Извлекает параметры команды из сообщения, начиная с позиции после команды.
    /// </summary>
    /// <param name="message">Сообщение из чата.</param>
    /// <param name="commandLength">Длина команды в сообщении.</param>
    /// <returns>Параметры команды в виде строки.</returns>
    private static string ExtractParameters(string message, int commandLength)
    {
        return message[commandLength..].Trim();
    }
}