using System.Collections.Concurrent;
using System.Text.Json;

namespace AbsoluteBot.Services.UserManagementServices;

/// <summary>
/// Кэш соответствий для SBTI: Игра → SBTI → Персонаж. Хранение в JSON.
/// </summary>
public class JsonSbtiCharacterCatalogService : IAsyncInitializable
{
    private const string FilePath = "sbti_characters.json";
    private static readonly SemaphoreSlim Semaphore = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new() {WriteIndented = true};
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

    public async Task<IReadOnlyCollection<string>> GetCharactersForTitleAsync(string title, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title)) return Array.Empty<string>();
        var tk = TitleKey(title);

        await Semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return _map.TryGetValue(tk, out var bySbti) ? bySbti.Values.ToArray() : Array.Empty<string>();
        }
        finally
        {
            Semaphore.Release();
        }
    }

    public async Task<bool> SaveCharacterAsync(string title, string sbti, string character, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(sbti) || string.IsNullOrWhiteSpace(character))
            return false;

        var gk = TitleKey(title);
        var sk = SbtiKey(sbti);
        var value = character.Trim();

        await Semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var bySbti = _map.GetOrAdd(gk, _ => new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase));
            if (bySbti.TryGetValue(sk, out var existing) && !string.IsNullOrWhiteSpace(existing)) return true;

            bySbti[sk] = value;
            await SaveAsync(_map).ConfigureAwait(false);
            return true;
        }
        finally
        {
            Semaphore.Release();
        }
    }

    public async Task<string?> TryGetCharacterAsync(string title, string sbti, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(sbti)) return null;

        var gk = TitleKey(title);
        var sk = SbtiKey(sbti);

        await Semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_map.TryGetValue(gk, out var bySbti) && bySbti.TryGetValue(sk, out var character) && !string.IsNullOrWhiteSpace(character))
                return character;
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
                return new ConcurrentDictionary<string, ConcurrentDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            var json = await File.ReadAllTextAsync(FilePath).ConfigureAwait(false);
            var data = JsonSerializer.Deserialize<ConcurrentDictionary<string, ConcurrentDictionary<string, string>>>(json, JsonOptions) ??
                       new ConcurrentDictionary<string, ConcurrentDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in data.ToArray()) data[kv.Key] = new ConcurrentDictionary<string, string>(kv.Value, StringComparer.OrdinalIgnoreCase);
            return data;
        }
        catch
        {
            return new ConcurrentDictionary<string, ConcurrentDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static async Task SaveAsync(ConcurrentDictionary<string, ConcurrentDictionary<string, string>> map)
    {
        var tempFilePath = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFilePath, JsonSerializer.Serialize(map, JsonOptions)).ConfigureAwait(false);
        await Task.Run(() => File.Move(tempFilePath, FilePath, true)).ConfigureAwait(false);
    }

    private static string SbtiKey(string sbti)
    {
        return sbti.Trim().ToUpperInvariant();
    }

    private static string TitleKey(string title)
    {
        return title.Trim().ToLowerInvariant();
    }
}