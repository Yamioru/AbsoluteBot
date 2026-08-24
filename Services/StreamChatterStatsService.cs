using System.Collections.Concurrent;
using System.Text.Encodings.Web;
using System.Text.Json;
using AbsoluteBot.Models;
using AbsoluteBot.Services.UtilityServices;
using Serilog;

namespace AbsoluteBot.Services;

/// <summary>
///     Считает сообщения чаттеров за сессию стрима (Twitch + VkPlayLive).
///     Сессия привязана к <c>StreamNumber</c>: продолжается после окончания Twitch, пока не начнётся следующий стрим.
/// </summary>
public class StreamChatterStatsService(ConfigService configService) : IAsyncInitializable
{
    private const string StatsDirectoryName = "stream_stats";
    private static readonly SemaphoreSlim Semaphore = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly ConcurrentDictionary<string, StreamChatterCount> _counts = new(StringComparer.OrdinalIgnoreCase);
    private int _streamNumber;

    public async Task InitializeAsync()
    {
        await Semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            _streamNumber = await configService.GetConfigValueAsync<int>("StreamNumber").ConfigureAwait(false);
            await LoadUnlockedAsync().ConfigureAwait(false);
            if (File.Exists(GetStatsFilePath(_streamNumber)))
                await SaveUnlockedAsync().ConfigureAwait(false);
        }
        finally
        {
            Semaphore.Release();
        }
    }

    /// <summary>
    ///     Закрывает текущую сессию (файл уже на диске) и начинает пустую сессию с новым номером стрима.
    /// </summary>
    /// <param name="newStreamNumber">Номер нового стрима.</param>
    public async Task BeginNewStreamAsync(int newStreamNumber)
    {
        await Semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (newStreamNumber == _streamNumber) return;

            await SaveUnlockedAsync().ConfigureAwait(false);
            _counts.Clear();
            _streamNumber = newStreamNumber;
            await SaveUnlockedAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при начале новой сессии статистики стрима {StreamNumber}.", newStreamNumber);
        }
        finally
        {
            Semaphore.Release();
        }
    }

    /// <summary>
    ///     Учитывает одно сообщение чаттера в текущей сессии стрима и сохраняет файл.
    /// </summary>
    /// <param name="nickname">Никнейм чаттера (Twitch DisplayName или VK Username).</param>
    public async Task RecordMessageAsync(string? nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname)) return;

        await Semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            _counts.AddOrUpdate(
                nickname,
                key => new StreamChatterCount {Nickname = key, Count = 1},
                (_, existing) =>
                {
                    existing.Count++;
                    return existing;
                });
            await SaveUnlockedAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при записи статистики сообщений для {Nickname}.", nickname);
        }
        finally
        {
            Semaphore.Release();
        }
    }

    /// <summary>
    ///     Снимок текущих счётчиков, отсортированный по убыванию количества сообщений.
    /// </summary>
    internal IReadOnlyList<StreamChatterCount> GetChattersSnapshot()
    {
        return _counts.Values
            .OrderByDescending(c => c.Count)
            .ThenBy(c => c.Nickname, StringComparer.OrdinalIgnoreCase)
            .Select(c => new StreamChatterCount {Nickname = c.Nickname, Count = c.Count})
            .ToList();
    }

    internal int CurrentStreamNumber => _streamNumber;

    internal static string GetStatsFilePath(int streamNumber)
    {
        return DataPaths.Get(Path.Combine(StatsDirectoryName, $"stream_{streamNumber}.json"));
    }

    private async Task LoadUnlockedAsync()
    {
        _counts.Clear();
        var path = GetStatsFilePath(_streamNumber);
        if (!File.Exists(path)) return;

        try
        {
            var json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
            var document = JsonSerializer.Deserialize<StreamChatterStatsDocument>(json, JsonOptions);
            if (document?.Chatters == null) return;

            foreach (var chatter in document.Chatters)
            {
                if (string.IsNullOrWhiteSpace(chatter.Nickname) || chatter.Count <= 0) continue;
                _counts[chatter.Nickname] = new StreamChatterCount {Nickname = chatter.Nickname, Count = chatter.Count};
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Ошибка при загрузке статистики стрима {StreamNumber}.", _streamNumber);
        }
    }

    private async Task SaveUnlockedAsync()
    {
        var path = GetStatsFilePath(_streamNumber);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var document = new StreamChatterStatsDocument
        {
            StreamNumber = _streamNumber,
            Chatters = GetChattersSnapshot().ToList()
        };
        var tempFilePath = Path.GetTempFileName();
        await using (var stream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await JsonSerializer.SerializeAsync(stream, document, JsonOptions).ConfigureAwait(false);
        }

        await Task.Run(() => File.Move(tempFilePath, path, true)).ConfigureAwait(false);
    }
}
