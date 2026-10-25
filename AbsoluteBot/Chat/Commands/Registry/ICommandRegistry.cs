namespace AbsoluteBot.Chat.Commands.Registry;

/// <summary>
///     Интерфейс для реестра команд чата, предоставляющий методы для поиска, получения и регистрации команд.
/// </summary>
public interface ICommandRegistry
{
    /// <summary>
    ///     Ищет команду по ее имени.
    /// </summary>
    /// <param name="commandName">Имя команды для поиска.</param>
    /// <returns>Команда, соответствующая заданному имени, или <c>null</c>, если команда не найдена.</returns>
    IChatCommand? FindCommand(string commandName);

    /// <summary>
    ///     Ищет и возвращает первую команду заданного типа.
    /// </summary>
    /// <typeparam name="T">Тип команды для поиска.</typeparam>
    /// <returns>Команда соответствующего типа или <c>null</c>, если команда не найдена.</returns>
    public T? FindCommandByType<T>() where T : class, IChatCommand;

    /// <summary>
    ///     Возвращает все зарегистрированные команды.
    /// </summary>
    /// <returns>Коллекция всех зарегистрированных команд.</returns>
    IEnumerable<IChatCommand> GetAllCommands();

    /// <summary>
    ///     Регистрирует новую команду в реестре.
    /// </summary>
    /// <param name="command">Команда для регистрации.</param>
    void RegisterCommand(IChatCommand command);
}