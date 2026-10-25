using System.Collections.Concurrent;
using Serilog;

namespace AbsoluteBot.Services.CommandManagementServices;

/// <summary>
///     Сервис для работы с статусом работы команд.
/// </summary>
public class CommandStatusService : IAsyncInitializable
{
    private const bool DefaultIsCommandEnabled = true;
    private ConcurrentDictionary<(string CommandName, string PlatformName), bool> _commandStatuses = new();

    public async Task InitializeAsync()
    {
        _commandStatuses = await InitializeStatuses().ConfigureAwait(false);
    }

    /// <summary>
    ///     Возвращает все статусы команд.
    /// </summary>
    /// <returns>Коллекция всех статусов команд.</returns>
    public virtual ConcurrentDictionary<(string, string), bool> GetAllCommandStatuses()
    {
        return _commandStatuses;
    }

    /// <summary>
    ///     Проверяет, включена ли команда для указанной платформы.
    /// </summary>
    /// <param name="commandName">Название команды.</param>
    /// <param name="platformName">Название платформы, для которой проверяется команда.</param>
    /// <returns>Возвращает true, если команда включена, иначе false.</returns>
    public virtual async Task<bool> IsCommandEnabled(string commandName, string platformName)
    {
        try
        {
            commandName = NormalizeCommand(commandName);
            var cacheKey = (CommandName: commandName, PlatformName: platformName);

            // Проверяется, есть ли команда в памяти
            if (_commandStatuses.TryGetValue(cacheKey, out var isEnabled)) return isEnabled;

            _commandStatuses[cacheKey] = DefaultIsCommandEnabled;
            await SaveCommandStatuses().ConfigureAwait(false);
            return DefaultIsCommandEnabled;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при получении статуса включенности команды.");
            return DefaultIsCommandEnabled;
        }
    }

    /// <summary>
    ///     Асинхронно устанавливает статус команды (включена/выключена) для указанной платформы.
    ///     Обновляет значение и сохраняет конфигурацию.
    /// </summary>
    /// <param name="commandName">Название команды.</param>
    /// <param name="platformName">Название платформы.</param>
    /// <param name="isEnabled">Статус команды (включена/выключена).</param>
    /// <returns>Возвращает true, если операция прошла успешно, иначе false.</returns>
    public virtual async Task<bool> SetCommandStatusAsync(string commandName, string platformName, bool isEnabled)
    {
        try
        {
            commandName = NormalizeCommand(commandName);
            var cacheKey = (CommandName: commandName, PlatformName: platformName);

            // Обновление значения
            _commandStatuses[cacheKey] = isEnabled;

            await SaveCommandStatuses().ConfigureAwait(false);

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при смене статуса работы команды на платформе: {platformName}", platformName);
            return false;
        }
    }

    /// <summary>
    ///     Инициализация статусов команд при старте сервиса.
    /// </summary>
    private static async Task<ConcurrentDictionary<(string CommandName, string PlatformName), bool>> InitializeStatuses()
    {
        return new ConcurrentDictionary<(string CommandName, string PlatformName), bool>(
            (await CommandFileService.LoadCommandStatusesAsync().ConfigureAwait(false))
            .SelectMany(command =>
                command.Value.Select(platformStatus =>
                    new KeyValuePair<(string, string), bool>(
                        (command.Key, platformStatus.Key), platformStatus.Value))));
    }

    /// <summary>
    ///     Нормализует команду, добавляя в начало '!', если его нет.
    /// </summary>
    /// <param name="command">Имя команды.</param>
    /// <returns>Нормализованная команда с '!' в начале.</returns>
    private static string NormalizeCommand(string command)
    {
        return command.StartsWith('!') ? command : '!' + command;
    }

    /// <summary>
    ///     Асинхронно преобразует данные в память обратно в формат для сохранения в файл и сохраняет.
    /// </summary>
    private async Task SaveCommandStatuses()
    {
        var commands = _commandStatuses
            .GroupBy(kvp => kvp.Key.CommandName)
            .ToDictionary(
                g => g.Key,
                g => g.ToDictionary(kvp => kvp.Key.PlatformName, kvp => kvp.Value));
        await CommandFileService.SaveCommandStatusesAsync(commands).ConfigureAwait(false);
    }
}