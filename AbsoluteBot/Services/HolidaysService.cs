using System.Collections.Concurrent;
using System.Text.Json;
using AbsoluteBot.Services.UtilityServices;
using Serilog;

namespace AbsoluteBot.Services;

/// <summary>
///     Сервис для управления праздниками, которые хранятся в локальном файле.
///     Позволяет получать информацию о праздниках по дате.
/// </summary>
public class HolidaysService : IAsyncInitializable
{
    private static string FilePath => DataPaths.Get("holidays.json");
    private const string NoHolidayMessage = "Сегодня нет праздника.";
    private static readonly SemaphoreSlim Semaphore = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private ConcurrentDictionary<string, string> _holidays = new();

    public async Task InitializeAsync()
    {
        _holidays = await LoadHolidaysAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     Возвращает праздник по заданной дате.
    ///     Если праздник на указанную дату не найден, возвращается сообщение о том, что праздников нет.
    /// </summary>
    /// <param name="date">Дата в формате "дд.мм".</param>
    /// <returns>Название праздника или сообщение о том, что праздников нет.</returns>
    public virtual string GetHoliday(string date)
    {
        try
        {
            return _holidays.TryGetValue(date, out var holiday) ? holiday : NoHolidayMessage;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при получении праздника.");
            return NoHolidayMessage;
        }
    }

    /// <summary>
    ///     Создает и возвращает словарь с примерными праздниками для инициализации.
    /// </summary>
    /// <returns>Словарь с примерными праздниками.</returns>
    private static ConcurrentDictionary<string, string> GenerateDefaultHolidays()
    {
        var holidays = new ConcurrentDictionary<string, string>();
        holidays.TryAdd("01.01", "Новый Год");
        holidays.TryAdd("08.03", "Международный женский день");
        return holidays;
    }

    /// <summary>
    ///     Асинхронно загружает информацию о праздниках из файла.
    /// </summary>
    private static async Task<ConcurrentDictionary<string, string>> LoadHolidaysAsync()
    {
        await Semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!File.Exists(FilePath))
            {
                Log.Warning("Не удалось загрузить список праздников, создание нового.");
                var defaultHolidays = GenerateDefaultHolidays();
                await SaveHolidaysAsync(defaultHolidays).ConfigureAwait(false);
                return defaultHolidays;
            }

            var json = await File.ReadAllTextAsync(FilePath).ConfigureAwait(false);
            return JsonSerializer.Deserialize<ConcurrentDictionary<string, string>>(json) ?? GenerateDefaultHolidays();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Ошибка при загрузке праздников из файла.");
            return GenerateDefaultHolidays();
        }
        finally
        {
            Semaphore.Release();
        }
    }

    /// <summary>
    ///     Асинхронно сохраняет праздники в файл.
    /// </summary>
    /// <param name="holidays">Словарь с праздниками.</param>
    private static async Task SaveHolidaysAsync(ConcurrentDictionary<string, string> holidays)
    {
        var json = JsonSerializer.Serialize(holidays, JsonOptions);
        var tempFilePath = Path.GetTempFileName();

        await File.WriteAllTextAsync(tempFilePath, json).ConfigureAwait(false);
        await Task.Run(() => File.Move(tempFilePath, FilePath, true)).ConfigureAwait(false);
    }
}