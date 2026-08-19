using AbsoluteBot.Models;
using AbsoluteBot.Services.UtilityServices;
using Newtonsoft.Json;
using Serilog;

namespace AbsoluteBot.Services;

/// <summary>
/// Сервис для работы с клипами, которые хранятся в локальном файле.
/// Позволяет искать клипы по описанию.
/// </summary>
public class ClipsService : IAsyncInitializable
{
    private static string FilePath => DataPaths.Get("clips.json");
    private static readonly SemaphoreSlim Semaphore = new(1, 1);
    private List<Clip> _clips = new();

    public async Task InitializeAsync()
    {
        _clips = await LoadClipsAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Добавляет новый клип в хранилище.
    /// </summary>
    public async Task AddClipAsync(Clip clip)
    {
        if (clip == null) throw new ArgumentNullException(nameof(clip));

        _clips.Add(clip);
        await SaveClipsAsync(_clips).ConfigureAwait(false);
    }

    /// <summary>
    /// Возвращает список всех клипов.
    /// </summary>
    public IEnumerable<Clip> GetAllClips()
    {
        return _clips;
    }

    private static async Task<List<Clip>> LoadClipsAsync()
    {
        if (!File.Exists(FilePath))
        {
            Log.Warning("Файл клипов не найден. Создаётся пустой список.");
            return new List<Clip>();
        }

        var json = await File.ReadAllTextAsync(FilePath).ConfigureAwait(false);
        return JsonConvert.DeserializeObject<List<Clip>>(json) ?? new List<Clip>();
    }

    private static async Task SaveClipsAsync(List<Clip> clips)
    {
        var json = JsonConvert.SerializeObject(clips, Formatting.Indented);
        var tempFilePath = Path.GetTempFileName();

        await File.WriteAllTextAsync(tempFilePath, json).ConfigureAwait(false);
        await Task.Run(() => File.Move(tempFilePath, FilePath, true)).ConfigureAwait(false);
    }
}