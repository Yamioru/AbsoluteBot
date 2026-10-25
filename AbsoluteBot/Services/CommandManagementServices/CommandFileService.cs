using System.Text.Json;
using AbsoluteBot.Services.UtilityServices;
using Serilog;

namespace AbsoluteBot.Services.CommandManagementServices;

#pragma warning disable IDE0028

/// <summary>
///     Сервис для работы с файлом, содержащим статусы команд.
///     Предоставляет методы для загрузки и сохранения статусов команд в файл.
/// </summary>
public static class CommandFileService
{
    private static string CommandFilePath => DataPaths.Get("commands.json");
    private static readonly SemaphoreSlim Semaphore = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    ///     Асинхронно загружает статусы команд из файла.
    /// </summary>
    /// <returns>Словарь с командами и их статусами.</returns>
    public static async Task<Dictionary<string, Dictionary<string, bool>>> LoadCommandStatusesAsync()
    {
        await Semaphore.WaitAsync().ConfigureAwait(false); // Ожидание свободного доступа к ресурсу
        try
        {
            if (!File.Exists(CommandFilePath))
            {
                Log.Warning("Не удалось загрузить список статусов включения у команд, создание нового.");
                // Если файл не существует, создание его и возвращение пустого словаря
                var emptyData = JsonSerializer.Serialize(new Dictionary<string, Dictionary<string, bool>>());
                await File.WriteAllTextAsync(CommandFilePath, emptyData).ConfigureAwait(false);
                return new Dictionary<string, Dictionary<string, bool>>();
            }

            var json = await File.ReadAllTextAsync(CommandFilePath).ConfigureAwait(false);
            return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, bool>>>(json)
                   ?? new Dictionary<string, Dictionary<string, bool>>();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при загрузке статусов команд из файла.");
            return new Dictionary<string, Dictionary<string, bool>>();
        }
        finally
        {
            Semaphore.Release(); // Освобождение доступа к файлу
        }
    }

    /// <summary>
    ///     Асинхронно сохраняет статусы команд в файл.
    /// </summary>
    /// <param name="commands">Словарь с командами и их статусами.</param>
    public static async Task SaveCommandStatusesAsync(Dictionary<string, Dictionary<string, bool>> commands)
    {
        await Semaphore.WaitAsync().ConfigureAwait(false); // Ожидание свободного доступа к ресурсу
        try
        {
            var json = JsonSerializer.Serialize(commands, JsonOptions);
            var tempFilePath = Path.GetTempFileName();

            await File.WriteAllTextAsync(tempFilePath, json).ConfigureAwait(false);
            await Task.Run(() => File.Move(tempFilePath, CommandFilePath, true)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при сохранении статусов команд в файл.");
        }
        finally
        {
            Semaphore.Release(); // Освобождение доступа к файлу
        }
    }
}