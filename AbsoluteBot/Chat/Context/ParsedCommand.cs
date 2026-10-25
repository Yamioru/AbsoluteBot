using AbsoluteBot.Models;

namespace AbsoluteBot.Chat.Context;

/// <summary>
///     Представляет разобранную команду, полученную в чате, с параметрами, контекстом и ролью пользователя.
/// </summary>
/// <param name="command">Команда, которая была введена пользователем.</param>
/// <param name="parameters">Параметры, переданные вместе с командой.</param>
/// <param name="context">Контекст чата, в котором была введена команда.</param>
/// <param name="userRole">
///     Роль пользователя, который ввел команду (например, админ, модератор, игнорируемый пользователь,
///     бот).
/// </param>
public class ParsedCommand(string command, string parameters, ChatContext context, UserRole userRole)
{
    /// <summary>
    ///     Контекст чата, в котором была введена команда.
    /// </summary>
    public ChatContext Context { get; set; } = context;
    /// <summary>
    ///     Команда, которая была введена пользователем.
    /// </summary>
    public string Command { get; set; } = command;
    /// <summary>
    ///     Параметры, переданные вместе с командой.
    /// </summary>
    public string Parameters { get; set; } = parameters;
    /// <summary>
    ///     Ответ, который будет отправлен пользователю (опционально).
    /// </summary>
    public string? Response { get; set; }
    /// <summary>
    ///     Роль пользователя, который ввел команду (например, админ, модератор, игнорируемый пользователь, бот).
    /// </summary>
    public UserRole UserRole { get; set; } = userRole;
}