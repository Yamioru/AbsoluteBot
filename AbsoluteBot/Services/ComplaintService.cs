using System.Text.Json;
using AbsoluteBot.Services.UtilityServices;
using Serilog;

namespace AbsoluteBot.Services;

#pragma warning disable IDE0028
/// <summary>
///     Сервис для работы с жалобами, включает добавление и получение жалоб.
/// </summary>
public class ComplaintService : IAsyncInitializable
{
    private static string FilePath => DataPaths.Get("complaints.json");
    private const int DefaultComplaintCount = 10;
    private static readonly SemaphoreSlim Semaphore = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private List<string> _complaints = new();

    public async Task InitializeAsync()
    {
        _complaints = await LoadComplaintsAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     Асинхронно добавляет жалобу в список и сохраняет обновления в файл.
    /// </summary>
    /// <param name="complaint">Текст жалобы, которую нужно добавить.</param>
    /// <returns>Возвращает <c>true</c>, если жалоба была успешно добавлена; <c>false</c> в случае ошибки.</returns>
    public virtual async Task<bool> AddComplaintAsync(string complaint)
    {
        try
        {
            _complaints.Add(complaint);
            await SaveComplaintsAsync(_complaints).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при добавлении жалобы.");
            return false;
        }
    }

    /// <summary>
    ///     Возвращает последние жалобы, ограниченные указанным количеством.
    /// </summary>
    /// <param name="count">Количество последних жалоб для возврата. По умолчанию <see cref="DefaultComplaintCount" />.</param>
    /// <returns>Список последних жалоб в виде строк.</returns>
    public virtual List<string> GetLastComplaints(int count = DefaultComplaintCount)
    {
        return _complaints.TakeLast(count).ToList();
    }

    /// <summary>
    ///     Асинхронно загружает жалобы из файла.
    /// </summary>
    private static async Task<List<string>> LoadComplaintsAsync()
    {
        await Semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!File.Exists(FilePath))
            {
                Log.Warning("Не удалось загрузить список жалоб пользователей, создание нового.");
                return new List<string>();
            }

            var json = await File.ReadAllTextAsync(FilePath).ConfigureAwait(false);
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Ошибка при загрузке жалоб из файла.");
            return new List<string>();
        }
        finally
        {
            Semaphore.Release();
        }
    }

    /// <summary>
    ///     Асинхронно сохраняет текущий список жалоб в файл.
    /// </summary>
    /// <param name="complaints">Список жалоб.</param>
    private static async Task SaveComplaintsAsync(List<string> complaints)
    {
        await Semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            var json = JsonSerializer.Serialize(complaints, JsonOptions);
            var tempFilePath = Path.GetTempFileName();

            await File.WriteAllTextAsync(tempFilePath, json).ConfigureAwait(false);
            await Task.Run(() => File.Move(tempFilePath, FilePath, true)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при сохранении жалоб в файл.");
        }
        finally
        {
            Semaphore.Release();
        }
    }
}