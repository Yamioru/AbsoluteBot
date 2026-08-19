using System.Collections.Concurrent;
using System.Text.Json;
using Serilog;

namespace AbsoluteBot.Services.UtilityServices;

#pragma warning disable IDE0028
/// <summary>
///     Сервис для управления списком цензурных слов. Позволяет добавлять, удалять и сохранять слова в файл.
/// </summary>
public class CensorWordsService : IAsyncInitializable
{
    private static string FilePath => DataPaths.Get("censor_words.json");
    private static readonly SemaphoreSlim FileSemaphore = new(1, 1);
    private static readonly SemaphoreSlim ListSemaphore = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private List<string> _censorWords = new();

    public async Task InitializeAsync()
    {
        _censorWords = await LoadCensorWordsAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     Асинхронно добавляет новое слово в список цензурных слов и сохраняет изменения.
    /// </summary>
    /// <param name="word">Цензурное слово, которое нужно добавить.</param>
    /// <returns>True, если слово успешно добавлено; иначе False.</returns>
    public virtual async Task<bool> AddCensorWordAsync(string word)
    {
        await ListSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_censorWords.Contains(word, StringComparer.InvariantCultureIgnoreCase)) return false;
            _censorWords.Add(word.ToLower());
            await SaveCensorWordsAsync(_censorWords).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при добавлении цензурного слова.");
            return false;
        }
        finally
        {
            ListSemaphore.Release();
        }
    }

    /// <summary>
    ///     Возвращает список всех цензурных слов.
    /// </summary>
    /// <returns>Список цензурных слов.</returns>
    public virtual ConcurrentBag<string> GetAllCensorWords()
    {
        return new ConcurrentBag<string>(_censorWords);
    }

    /// <summary>
    ///     Асинхронно удаляет слово из списка цензурных слов и сохраняет изменения.
    /// </summary>
    /// <param name="word">Цензурное слово, которое нужно удалить.</param>
    /// <returns>True, если слово успешно удалено; иначе False.</returns>
    public virtual async Task<bool> RemoveCensorWordAsync(string word)
    {
        await ListSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_censorWords.Remove(word.ToLower())) return false;
            await SaveCensorWordsAsync(_censorWords).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при удалении цензурного слова.");
            return false;
        }
        finally
        {
            ListSemaphore.Release();
        }
    }

    /// <summary>
    ///     Асинхронно загружает список цензурных слов из файла.
    /// </summary>
    private static async Task<List<string>> LoadCensorWordsAsync()
    {
        await FileSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!File.Exists(FilePath))
            {
                Log.Warning("Не удалось загрузить список цензурируемых слов, создание нового.");
                var emptyList = new List<string>();
                var initialBirthdaysJson = JsonSerializer.Serialize(emptyList, JsonOptions);
                await File.WriteAllTextAsync(FilePath, initialBirthdaysJson).ConfigureAwait(false);
                return emptyList;
            }

            var json = await File.ReadAllTextAsync(FilePath).ConfigureAwait(false);
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Ошибка при загрузке списка цензурных слов.");
            return new List<string>();
        }
        finally
        {
            FileSemaphore.Release();
        }
    }

    /// <summary>
    ///     Асинхронно сохраняет текущий список цензурных слов в файл.
    /// </summary>
    private static async Task SaveCensorWordsAsync(List<string> censorWords)
    {
        await FileSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            var json = JsonSerializer.Serialize(censorWords, JsonOptions);
            var tempFilePath = Path.GetTempFileName();

            await File.WriteAllTextAsync(tempFilePath, json).ConfigureAwait(false);
            await Task.Run(() => File.Move(tempFilePath, FilePath, true)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при сохранении списка цензурных слов в файл.");
        }
        finally
        {
            FileSemaphore.Release();
        }
    }
}