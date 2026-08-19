using System.Collections.Concurrent;
using System.Text.Json;
using AbsoluteBot.Services.UtilityServices;
using Serilog;

namespace AbsoluteBot.Services.CommandManagementServices;

/// <summary>
///     Сервис для управления перезарядкой (cooldown) команд.
/// </summary>
public class CooldownService : IAsyncInitializable
{
    private static string CooldownFilePath => DataPaths.Get("cooldowns.json");
    private static readonly SemaphoreSlim Semaphore = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly ConcurrentDictionary<string, DateTime> _lastUsedTimes = new();
    private ConcurrentDictionary<string, int> _cooldownDurations = new();

    public async Task InitializeAsync()
    {
        _cooldownDurations = await LoadCooldownDurationsAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     Проверяет, находится ли команда на перезарядке для заданного сервиса.
    /// </summary>
    /// <param name="serviceName">Имя сервиса (платформы).</param>
    /// <returns><c>true</c>, если команда на перезарядке; в противном случае — <c>false</c>.</returns>
    public bool IsOnCooldown(string serviceName)
    {
        if (!_lastUsedTimes.TryGetValue(serviceName, out var lastUsed)) return false;
        var cooldownDuration = _cooldownDurations.TryGetValue(serviceName, out var duration) ? duration : 0;
        return DateTime.Now < lastUsed.AddSeconds(cooldownDuration);
    }

    /// <summary>
    ///     Асинхронно устанавливает длительность перезарядки для заданного сервиса.
    /// </summary>
    /// <param name="serviceName">Имя сервиса (платформы).</param>
    /// <param name="seconds">Длительность перезарядки в секундах.</param>
    /// <returns><c>true</c>, если перезарядка была успешно установлена; в противном случае — <c>false</c>.</returns>
    public virtual async Task<bool> SetCooldownAsync(string serviceName, string seconds)
    {
        try
        {
            if (!int.TryParse(seconds, out var intSeconds)) return false;
            _cooldownDurations[serviceName] = intSeconds;
            await SaveCooldownDurationsAsync(_cooldownDurations).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при установке перезарядки.");
            return false;
        }
    }

    /// <summary>
    ///     Устанавливает время последнего использования команды для заданного сервиса.
    /// </summary>
    /// <param name="serviceName">Имя сервиса (платформы).</param>
    /// <returns><c>true</c>, если время было успешно установлено; в противном случае — <c>false</c>.</returns>
    public bool SetLastUsed(string serviceName)
    {
        try
        {
            _lastUsedTimes[serviceName] = DateTime.Now;
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при установке последнего использования команды.");
            return false;
        }
    }

    /// <summary>
    ///     Асинхронно загружает длительности перезарядки из файла.
    /// </summary>
    private static async Task<ConcurrentDictionary<string, int>> LoadCooldownDurationsAsync()
    {
        await Semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!File.Exists(CooldownFilePath))
            {
                Log.Warning("Не удалось загрузить список перезарядок команд, создание нового.");
                return new ConcurrentDictionary<string, int>();
            }

            var cooldownsJson = await File.ReadAllTextAsync(CooldownFilePath).ConfigureAwait(false);
            return JsonSerializer.Deserialize<ConcurrentDictionary<string, int>>(cooldownsJson) ?? new ConcurrentDictionary<string, int>();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Ошибка при загрузке данных о перезарядках из файла.");
            return new ConcurrentDictionary<string, int>();
        }
        finally
        {
            Semaphore.Release();
        }
    }

    /// <summary>
    ///     Асинхронно сохраняет длительности перезарядки в файл.
    /// </summary>
    /// <param name="cooldownDurations">Словарь перязарядок для платформ</param>
    private static async Task SaveCooldownDurationsAsync(ConcurrentDictionary<string, int> cooldownDurations)
    {
        await Semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            var json = JsonSerializer.Serialize(cooldownDurations, JsonOptions);
            var tempFilePath = Path.GetTempFileName();

            await File.WriteAllTextAsync(tempFilePath, json).ConfigureAwait(false);
            await Task.Run(() => File.Move(tempFilePath, CooldownFilePath, true)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при сохранении данных о перезарядках в файл.");
        }
        finally
        {
            Semaphore.Release();
        }
    }
}