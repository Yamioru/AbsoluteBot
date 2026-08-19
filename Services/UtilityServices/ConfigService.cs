using System.Collections.Concurrent;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace AbsoluteBot.Services.UtilityServices;

/// <summary>
///     Сервис для работы с файлами, содержащими статусы команд.
///     Отвечает за загрузку и сохранение статусов команд в файл.
/// </summary>
public class ConfigService : IAsyncInitializable
{
    private static string FilePath => DataPaths.Get("config.json");
    private static readonly SemaphoreSlim Semaphore = new(1, 1);
    private ConcurrentDictionary<string, object?> _config = new();

    public async Task InitializeAsync()
    {
        await Semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            _config = await LoadConfigAsync().ConfigureAwait(false);
        }
        finally
        {
            Semaphore.Release();
        }
    }

    /// <summary>
    ///     Возвращает все конфигурационные значения в виде словаря, с маскированием конфиденциальных данных.
    /// </summary>
    /// <returns>Словарь всех конфигурационных значений с маскированием.</returns>
    public virtual Dictionary<string, string> GetAllConfigValues()
    {
        var censoredConfig = new Dictionary<string, string>();

        foreach (var kvp in _config)
            censoredConfig[kvp.Key] = CensorValue(kvp.Value?.ToString());

        return censoredConfig;
    }

    /// <summary>
    ///     Асинхронно возвращает значение конфигурации для указанного ключа и типа.
    ///     Если значение отсутствует, сохраняется значение по умолчанию.
    /// </summary>
    /// <typeparam name="T">Тип возвращаемого значения.</typeparam>
    /// <param name="key">Ключ конфигурации.</param>
    /// <returns>Значение конфигурации указанного типа.</returns>
    public virtual async Task<T?> GetConfigValueAsync<T>(string key)
    {
        await Semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_config.TryGetValue(key, out var value))
                return ConvertConfigValue<T>(value);

            var defaultValue = GetDefaultValue<T>();
            _config[key] = defaultValue;
            await SaveConfigAsync(_config).ConfigureAwait(false);

            return defaultValue;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при получении значения из конфига по ключу " + key);
            return default;
        }
        finally
        {
            Semaphore.Release();
        }
    }

    /// <summary>
    ///     Асинхронно устанавливает значение конфигурации для указанного ключа.
    ///     Сохраняет изменения в файл.
    /// </summary>
    /// <param name="key">Ключ конфигурации.</param>
    /// <param name="value">Новое значение конфигурации.</param>
    /// <returns>Возвращает true, если операция прошла успешно, иначе false.</returns>
    public virtual async Task<bool> SetConfigValueAsync(string key, object value)
    {
        await Semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            _config[key] = value;
            await SaveConfigAsync(_config).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при задании значения в конфиге.");
            return false;
        }
        finally
        {
            Semaphore.Release();
        }
    }

    /// <summary>
    ///     Преобразует конфиденциальное значение в цензурированную строку (для маскировки данных).
    /// </summary>
    /// <param name="value">Конфиденциальное значение.</param>
    /// <returns>Цензурированное значение.</returns>
    private static string CensorValue(string? value)
    {
        if (value != null && value.Length > 4)
            return value[..2] + new string('*', value.Length - 4) + value[^2..];
        return value ?? string.Empty;
    }

    /// <summary>
    ///     Преобразует значение конфигурации в нужный тип.
    /// </summary>
    /// <typeparam name="T">Тип данных для преобразования.</typeparam>
    /// <param name="value">Значение для преобразования.</param>
    /// <returns>Преобразованное значение нужного типа.</returns>
    private static T ConvertConfigValue<T>(object? value)
    {
        if (value == null) return GetDefaultValue<T>();

        return typeof(T) switch
        {
            // Обработка для List<string>
            { } t when t == typeof(List<string>) => ConvertToListString<T>(value),

            _ => (T) Convert.ChangeType(value, typeof(T))
        };
    }

    /// <summary>
    ///     Преобразование значения в список строк типа List&lt;string&gt;.
    /// </summary>
    private static T ConvertToListString<T>(object value)
    {
        if (value is not JArray jArray) return GetDefaultValue<T>();
        var result = jArray.Select(item => item.ToString()).ToList();
        return (T) (object) result;
    }

    /// <summary>
    ///     Асинхронно создает новый конфигурационный файл с пустыми данными и сохраняет его.
    /// </summary>
    /// <returns>Пустой словарь, представляющий новую конфигурацию.</returns>
    private static async Task<ConcurrentDictionary<string, object?>> CreateNewConfigAsync()
    {
        var config = new ConcurrentDictionary<string, object?>();
        await SaveConfigAsync(config).ConfigureAwait(false);
        return config;
    }

    /// <summary>
    ///     Возвращает значение по умолчанию для указанного типа.
    /// </summary>
    /// <typeparam name="T">Тип данных, для которого нужно вернуть значение по умолчанию.</typeparam>
    /// <returns>Значение по умолчанию.</returns>
    private static T GetDefaultValue<T>()
    {
        object? defaultValue = typeof(T) switch
        {
            { } t when t == typeof(string) => string.Empty,
            { } t when t == typeof(int) => 0,
            { } t when t == typeof(bool) => true,
            { } t when t == typeof(List<string>) =>
                new List<string>(),
            _ => default(T)
        };
        return (T) defaultValue!;
    }

    /// <summary>
    ///     Асинхронно загружает конфигурационные данные из файла или создает новую конфигурацию, если файл отсутствует.
    /// </summary>
    /// <returns>Словарь с конфигурацией, либо пустой словарь, если файл не существует.</returns>
    private static async Task<ConcurrentDictionary<string, object?>> LoadConfigAsync()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                Log.Warning("Не удалось загрузить конфиг, создание нового.");
                return await CreateNewConfigAsync().ConfigureAwait(false);
            }

            var config = await TryReadConfigFromFileAsync().ConfigureAwait(false);
            return config;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Не удалось загрузить или создать конфиг.");
            throw;
        }
    }

    /// <summary>
    ///     Асинхронно сохраняет конфигурационные данные в файл.
    /// </summary>
    private static async Task SaveConfigAsync(ConcurrentDictionary<string, object?> config)
    {
        try
        {
            var json = JsonConvert.SerializeObject(config, Formatting.Indented);
            var tempFilePath = Path.GetTempFileName();

            await File.WriteAllTextAsync(tempFilePath, json).ConfigureAwait(false);
            await Task.Run(() => File.Move(tempFilePath, FilePath, true)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при сохранении конфигурации.");
        }
    }

    /// <summary>
    ///     Асинхронно пытается прочитать файл конфигурации и десериализовать его содержимое в словарь.
    ///     В случае ошибки возвращает пустой словарь.
    /// </summary>
    /// <returns>Словарь с конфигурацией, либо пустой словарь в случае ошибки.</returns>
    private static async Task<ConcurrentDictionary<string, object?>> TryReadConfigFromFileAsync()
    {
        try
        {
            var json = await File.ReadAllTextAsync(FilePath).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<ConcurrentDictionary<string, object?>>(json)
                   ?? new ConcurrentDictionary<string, object?>();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при чтении файла конфигурации.");
            return new ConcurrentDictionary<string, object?>();
        }
    }
}