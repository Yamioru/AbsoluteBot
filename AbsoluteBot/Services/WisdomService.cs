using System.Collections.Concurrent;
using System.Text.Json;
using AbsoluteBot.Services.UtilityServices;
using Serilog;

namespace AbsoluteBot.Services;

#pragma warning disable IDE0028
/// <summary>
///     Сервис для управления и получения случайных мудростей.
///     Мудрости загружаются из файла и могут быть добавлены в него.
/// </summary>
public class WisdomService : IAsyncInitializable
{
    private static string FilePath => DataPaths.Get("wisdoms.json");
    private static readonly SemaphoreSlim FileSemaphore = new(1, 1);
    private static readonly SemaphoreSlim ListSemaphore = new(1, 1);
    private static readonly Random Random = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private ConcurrentBag<string> _wisdoms = new();

    public async Task InitializeAsync()
    {
        _wisdoms = await LoadWisdomsAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     Асинхронно добавляет новую мудрость в список и сохраняет его в файл.
    /// </summary>
    /// <param name="wisdom">Мудрость, которая будет добавлена.</param>
    /// <returns>
    ///     <c>true</c>, если мудрость успешно добавлена; в противном случае <c>false</c>.
    /// </returns>
    public virtual async Task<bool> AddWisdomAsync(string wisdom)
    {
        await ListSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            _wisdoms.Add(wisdom);
            await SaveWisdomsAsync(_wisdoms).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при добавлении мудрости.");
            return false;
        }
        finally
        {
            ListSemaphore.Release();
        }
    }

    /// <summary>
    ///     Асинхронно возвращает случайную мудрость из списка.
    /// </summary>
    /// <returns>Случайная мудрость или <c>null</c>, если список пуст.</returns>
    public virtual async Task<string?> GetRandomWisdomAsync()
    {
        await ListSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            return _wisdoms.IsEmpty
                ? null
                : _wisdoms.ElementAt(Random.Next(_wisdoms.Count));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при получении мудрости.");
            return null;
        }
        finally
        {
            ListSemaphore.Release();
        }
    }

    /// <summary>
    ///     Асинхронно загружает список мудростей из файла.
    /// </summary>
    private static async Task<ConcurrentBag<string>> LoadWisdomsAsync()
    {
        await FileSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!File.Exists(FilePath))
            {
                Log.Warning("Не удалось загрузить список мудростей, создание нового.");
                return new ConcurrentBag<string>();
            }

            // Десериализация в List<string> и затем преобразование в ConcurrentBag<string>
            var json = await File.ReadAllTextAsync(FilePath).ConfigureAwait(false);
            var wisdomList = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            return new ConcurrentBag<string>(wisdomList);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Ошибка при загрузке мудростей из файла.");
            return new ConcurrentBag<string>();
        }
        finally
        {
            FileSemaphore.Release();
        }
    }

    /// <summary>
    ///     Асинхронно сохраняет список мудростей в файл.
    /// </summary>
    /// <param name="wisdoms">Список мудростей.</param>
    private static async Task SaveWisdomsAsync(ConcurrentBag<string> wisdoms)
    {
        await FileSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            var json = JsonSerializer.Serialize(wisdoms, JsonOptions);
            var tempFilePath = Path.GetTempFileName();

            await File.WriteAllTextAsync(tempFilePath, json).ConfigureAwait(false);
            await Task.Run(() => File.Move(tempFilePath, FilePath, true)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при сохранении мудростей в файл.");
        }
        finally
        {
            FileSemaphore.Release();
        }
    }
}