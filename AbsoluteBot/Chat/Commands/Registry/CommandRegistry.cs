using FuzzySharp;

namespace AbsoluteBot.Chat.Commands.Registry;

#pragma warning disable IDE0028

/// <summary>
///     Реестр команд чата, который хранит и управляет регистрацией команд.
///     Этот класс позволяет находить, получать и регистрировать команды, реализующие интерфейс <see cref="IChatCommand" />
/// </summary>
public class CommandRegistry : ICommandRegistry
{
    /// <summary>
    ///     Словарь, содержащий зарегистрированные команды чата, где ключом является имя команды в нижнем регистре.
    /// </summary>
    private readonly Dictionary<string, IChatCommand> _commands = new();

    /// <summary>
    ///     Ищет и возвращает команду по ее имени.
    /// </summary>
    /// <param name="commandName">Имя команды, которую нужно найти.</param>
    /// <returns>Объект <see cref="IChatCommand" />, если команда найдена; иначе <c>null</c>.</returns>
    public IChatCommand? FindCommand(string commandName)
    {
        commandName = commandName.ToLowerInvariant();

        // Попытка найти точное совпадение
        if (_commands.TryGetValue(commandName, out var command))
        {
            return command;
        }

        // Использование FuzzySharp для поиска наиболее близкой команды
        var closestMatch = _commands.Keys
            .OrderByDescending(key => Fuzz.Ratio(key, commandName))
            .FirstOrDefault(key => Fuzz.Ratio(key, commandName) >= 70); // Порог точности 70%

        if (closestMatch != null)
        {
            _commands.TryGetValue(closestMatch, out var similarCommand);
            return similarCommand;
        }

        return null;
    }

    /// <summary>
    ///     Возвращает все зарегистрированные команды.
    /// </summary>
    /// <returns>Коллекция всех команд, зарегистрированных в реестре.</returns>
    public IEnumerable<IChatCommand> GetAllCommands()
    {
        return _commands.Values;
    }

    /// <summary>
    ///     Ищет и возвращает первую команду заданного типа.
    /// </summary>
    /// <typeparam name="T">Тип команды для поиска.</typeparam>
    /// <returns>Команда соответствующего типа или <c>null</c>, если команда не найдена.</returns>
    public T? FindCommandByType<T>() where T : class, IChatCommand
    {
        return _commands.Values.OfType<T>().FirstOrDefault();
    }

    /// <summary>
    ///     Регистрирует команду в реестре, используя имя команды в качестве ключа.
    /// </summary>
    /// <param name="command">Команда, которую нужно зарегистрировать.</param>
    public void RegisterCommand(IChatCommand command)
    {
        _commands[command.Name.ToLowerInvariant()] = command;
    }
}