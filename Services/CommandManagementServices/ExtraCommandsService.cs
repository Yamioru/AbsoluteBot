using System.Collections.Concurrent;
using System.Text.Json;
using AbsoluteBot.Services.UtilityServices;
using Serilog;

namespace AbsoluteBot.Services.CommandManagementServices;

/// <summary>
///     Сервис для управления динамическими командами, которые могут быть добавлены или изменены пользователями.
/// </summary>
public class ExtraCommandsService : IAsyncInitializable
{
    private static string FilePath => DataPaths.Get("extra_commands.json");
    private static readonly SemaphoreSlim Semaphore = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private ConcurrentDictionary<string, string> _extraCommands = new();

    public async Task InitializeAsync()
    {
        _extraCommands = await LoadExtraCommandsAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     Асинхронно добавляет или обновляет динамическую команду.
    /// </summary>
    /// <param name="command">Имя команды.</param>
    /// <param name="response">Ответ, который будет возвращен при вызове команды.</param>
    /// <returns><c>true</c>, если команда успешно добавлена или обновлена; иначе <c>false</c>.</returns>
    public virtual async Task<bool> AddOrUpdateCommandAsync(string command, string response)
    {
        try
        {
            command = NormalizeCommand(command);
            _extraCommands[command] = response;
            await SaveExtraCommandsAsync(_extraCommands).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при добавлении динамической команды.");
            return false;
        }
    }

    /// <summary>
    ///     Возвращает список всех динамических команд.
    /// </summary>
    /// <returns>Коллекция имен команд.</returns>
    public IEnumerable<string> GetAllCommands()
    {
        return _extraCommands.Keys;
    }

    /// <summary>
    ///     Получает ответ, связанный с указанной командой.
    /// </summary>
    /// <param name="command">Имя команды.</param>
    /// <returns>Ответ команды или <c>null</c>, если команда не найдена.</returns>
    public virtual string? GetCommand(string command)
    {
        return _extraCommands.TryGetValue(command, out var result) ? result : null;
    }

    /// <summary>
    ///     Асинхронно удаляет указанную команду.
    /// </summary>
    /// <param name="command">Имя команды.</param>
    /// <returns><c>true</c>, если команда была успешно удалена; иначе <c>false</c>.</returns>
    public virtual async Task<bool> RemoveCommandAsync(string command)
    {
        command = NormalizeCommand(command);
        if (!_extraCommands.TryRemove(command, out _)) return false;
        await SaveExtraCommandsAsync(_extraCommands).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    ///     Асинхронно загружает команды из файла.
    /// </summary>
    private static async Task<ConcurrentDictionary<string, string>> LoadExtraCommandsAsync()
    {
        await Semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!File.Exists(FilePath))
            {
                Log.Warning("Не удалось загрузить список дополнительных команд, создание нового.");
                return new ConcurrentDictionary<string, string>();
            }

            var json = await File.ReadAllTextAsync(FilePath).ConfigureAwait(false);
            return JsonSerializer.Deserialize<ConcurrentDictionary<string, string>>(json) ?? new ConcurrentDictionary<string, string>();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Ошибка при загрузке динамических команд из файла.");
            return new ConcurrentDictionary<string, string>();
        }
        finally
        {
            Semaphore.Release();
        }
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
    ///     Асинхронно сохраняет команды в файл.
    /// </summary>
    private static async Task SaveExtraCommandsAsync(ConcurrentDictionary<string, string> extraCommands)
    {
        await Semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            var json = JsonSerializer.Serialize(extraCommands, JsonOptions);
            var tempFilePath = Path.GetTempFileName();

            await File.WriteAllTextAsync(tempFilePath, json).ConfigureAwait(false);
            await Task.Run(() => File.Move(tempFilePath, FilePath, true)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при сохранении динамических команд в файл.");
        }
        finally
        {
            Semaphore.Release();
        }
    }
}