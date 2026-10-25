using AbsoluteBot.Chat.Context;

namespace AbsoluteBot.Chat.Commands;

/// <summary>
///     Интерфейс, представляющий команду чата, которую можно выполнять.
/// </summary>
public interface IChatCommand
{
    /// <summary>
    ///     Приоритет вывода команды в списке команд.
    /// </summary>
    int Priority { get; }
    /// <summary>
    ///     Описание команды, используемое для вывода списка команд.
    /// </summary>
    string Description { get; }
    /// <summary>
    ///     Имя команды, которое используется для её вызова в чате.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///     Определяет, может ли команда быть выполнена на основе предоставленного контекста команды.
    /// </summary>
    /// <param name="command">Контекст команды, который содержит информацию о вызове команды.</param>
    /// <returns>Возвращает <c>true</c>, если команда может быть выполнена; в противном случае <c>false</c>.</returns>
    bool CanExecute(ParsedCommand command);

    /// <summary>
    ///     Выполняет логику команды асинхронно.
    /// </summary>
    /// <param name="command">Контекст команды, содержащий параметры и информацию для выполнения.</param>
    /// <returns>Возвращает результат выполнения команды в виде строки.</returns>
    Task<string> ExecuteAsync(ParsedCommand command);
}