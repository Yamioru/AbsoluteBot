using System.Collections.Concurrent;
using System.Text.Json;
using AbsoluteBot.Services.UtilityServices;
using Serilog;

namespace AbsoluteBot.Services.UserManagementServices;

/// <summary>
/// Кэш соответствий: Игра → MBTI → Персонаж. Хранение в JSON.
/// Ключ игры приводится к lower, ключ MBTI — к UPPER.
/// </summary>
public class JsonMbtiCharacterCatalogService : IAsyncInitializable
{
    private static string FilePath => DataPaths.Get("mbti_characters.json");
    private static readonly SemaphoreSlim Semaphore = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _map = new();

    public async Task InitializeAsync()
    {
        await Semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            _map = await LoadAsync().ConfigureAwait(false);
        }
        finally
        {
            Semaphore.Release();
        }
    }

    /// <summary>
    /// Вернуть все уже известные персонажи для тайтла (для исключений Gemini).
    /// <paramref name="title">Название тайтла</paramref>
    /// </summary>
    public async Task<IReadOnlyCollection<string>> GetCharactersForTitleAsync(string title, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Array.Empty<string>();

        var tk = TitleKey(title);

        await Semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return _map.TryGetValue(tk, out var byMbti) ? byMbti.Values.ToArray() : Array.Empty<string>();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при получении списка персонажей для тайтла.");
            return Array.Empty<string>();
        }
        finally
        {
            Semaphore.Release();
        }
    }

    /// <summary>
    /// Сохранить персонажа для набора (игра, MBTI). Не перезаписывает существующее значение.
    /// <paramref name="title"> Название тайтла</paramref>
    /// <paramref name="mbti"> MBTI персонажа</paramref>
    /// <paramref name="character"> Персонаж соответствующий такому тайтлу и mbti</paramref>
    /// </summary>
    public async Task<bool> SaveCharacterAsync(string title, string mbti, string character, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(mbti) || string.IsNullOrWhiteSpace(character))
            return false;

        var gk = TitleKey(title);
        var mk = MbtiKey(mbti);
        var value = character.Trim();

        await Semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // гарантируем под-словарь для игры
            var byMbti = _map.GetOrAdd(gk, _ => new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase));

            // не перезаписываем, если уже есть
            if (byMbti.TryGetValue(mk, out var existing) && !string.IsNullOrWhiteSpace(existing))
                return true;

            byMbti[mk] = value;

            await SaveAsync(_map).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при сохранении персонажа в JSON-кэш.");
            return false;
        }
        finally
        {
            Semaphore.Release();
        }
    }

    /// <summary>
    /// Вернуть персонажа для игры и MBTI или null.
    /// <paramref name="title">Название тайтла</paramref>
    /// <paramref name="mbti">MBTI персонажа</paramref>
    /// </summary>
    public async Task<string?> TryGetCharacterAsync(string title, string mbti, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(mbti))
            return null;

        var gk = TitleKey(title);
        var mk = MbtiKey(mbti);

        await Semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_map.TryGetValue(gk, out var byMbti) &&
                byMbti.TryGetValue(mk, out var character) &&
                !string.IsNullOrWhiteSpace(character))
                return character;

            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при получении персонажа из JSON-кэша.");
            return null;
        }
        finally
        {
            Semaphore.Release();
        }
    }

    private static async Task<ConcurrentDictionary<string, ConcurrentDictionary<string, string>>> LoadAsync()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                Log.Warning("Файл кэша MBTI-персонажей не найден, создаём новый.");
                var empty = new ConcurrentDictionary<string, ConcurrentDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
                await SaveAsync(empty).ConfigureAwait(false);
                return empty;
            }

            var json = await File.ReadAllTextAsync(FilePath).ConfigureAwait(false);

            var data = JsonSerializer.Deserialize<ConcurrentDictionary<string, ConcurrentDictionary<string, string>>>(
                           json, JsonOptions)
                       ?? new ConcurrentDictionary<string, ConcurrentDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

            // убедимся, что вложенные словари существуют и корректно настроены
            foreach (var kv in data.ToArray()) data[kv.Key] = kv.Value;

            return data;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Ошибка при загрузке JSON-кэша MBTI-персонажей. Используем пустой.");
            return new ConcurrentDictionary<string, ConcurrentDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string MbtiKey(string mbti)
    {
        return mbti.Trim().ToUpperInvariant();
    }

    private static async Task SaveAsync(ConcurrentDictionary<string, ConcurrentDictionary<string, string>> map)
    {
        try
        {
            var json = JsonSerializer.Serialize(map, JsonOptions);
            var tempFilePath = Path.GetTempFileName();

            await File.WriteAllTextAsync(tempFilePath, json).ConfigureAwait(false);
            await Task.Run(() => File.Move(tempFilePath, FilePath, true)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при сохранении JSON-кэша MBTI-персонажей.");
        }
    }

    private static string TitleKey(string title)
    {
        return title.Trim().ToLowerInvariant();
    }
}