using System.Collections.Concurrent;
using System.Text.Encodings.Web;
using System.Text.Json;
using AbsoluteBot.Models;
using AbsoluteBot.Services.NeuralNetworkServices;
using AbsoluteBot.Services.UserManagementServices;
using AbsoluteBot.Services.UtilityServices;
using Serilog;

namespace AbsoluteBot.Services;

/// <summary>
///     Тихий учёт ачивок по сообщениям Twitch и VK Live: классификация нейросетью, прогресс только в JSON.
/// </summary>
public class ChatAchievementService : IAsyncInitializable
{
    private const int LastMatchMaxLength = 200;
    private const string CatalogFileName = "achievements.json";
    private const string ProgressDirectoryName = "achievements";
    private const string ProgressFileName = "progress.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly IAchievementClassifier _classifier;
    private readonly RoleService _roleService;
    private readonly UserIdentityService? _userIdentityService;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ConcurrentDictionary<string, AchievementUserProgress> _users = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyDictionary<string, AchievementDefinition> _catalog = new Dictionary<string, AchievementDefinition>(StringComparer.OrdinalIgnoreCase);

    public ChatAchievementService(IAchievementClassifier classifier, RoleService roleService, UserIdentityService userIdentityService)
        : this(classifier, roleService, TimeProvider.System, userIdentityService)
    {
    }

    public ChatAchievementService(IAchievementClassifier classifier, RoleService roleService)
        : this(classifier, roleService, TimeProvider.System, null)
    {
    }

    public ChatAchievementService(IAchievementClassifier classifier, RoleService roleService, TimeProvider timeProvider)
        : this(classifier, roleService, timeProvider, null)
    {
    }

    public ChatAchievementService(IAchievementClassifier classifier, RoleService roleService, TimeProvider timeProvider,
        UserIdentityService? userIdentityService)
    {
        _classifier = classifier;
        _roleService = roleService;
        _timeProvider = timeProvider;
        _userIdentityService = userIdentityService;
    }

    public async Task InitializeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            LoadCatalog();
            await LoadProgressUnlockedAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    ///     Классифицирует сообщение и при необходимости начисляет баллы. Ошибки глотаются — чат не блокируется.
    /// </summary>
    public Task TryRecordAsync(string? nickname, string? text, string? platform) =>
        TryRecordAsync(nickname, text, platform, null);

    public async Task TryRecordAsync(string? nickname, string? text, string? platform, string? userId)
    {
        try
        {
            await RecordCoreAsync(nickname, text, platform, userId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при тихом учёте ачивки для {Nickname} ({Platform}).", nickname, platform);
        }
    }

    internal IReadOnlyList<AchievementDefinition> GetCatalogSnapshot() =>
        _catalog.Values.OrderBy(a => a.Id, StringComparer.Ordinal).ToList();

    internal IReadOnlyList<AchievementUserProgress> GetProgressSnapshot() =>
        _users.Values
            .OrderBy(u => u.Nickname, StringComparer.OrdinalIgnoreCase)
            .Select(CloneUser)
            .ToList();

    internal static string GetProgressFilePath() =>
        DataPaths.Get(Path.Combine(ProgressDirectoryName, ProgressFileName));

    private async Task RecordCoreAsync(string? nickname, string? text, string? platform, string? userId)
    {
        if (string.IsNullOrWhiteSpace(nickname) || string.IsNullOrWhiteSpace(text)) return;
        if (text.StartsWith('!')) return;
        if (_catalog.Count == 0) return;

        IReadOnlyList<string> aliases = new[] {nickname};
        if (_userIdentityService != null)
            aliases = await _userIdentityService.RememberAsync(nickname, platform, userId).ConfigureAwait(false);

        var role = _roleService.GetExistingUserRole(nickname, platform, userId);
        if (role is UserRole.Ignored or UserRole.Bot) return;

        IReadOnlyList<AchievementDefinition> catalog;
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_catalog.Count == 0) return;
            catalog = GetCatalogSnapshot();
        }
        finally
        {
            _gate.Release();
        }

        var ids = await _classifier.ClassifyAsync(text, catalog).ConfigureAwait(false);
        if (ids.Count == 0) return;

        Log.Information("Ачивки {Nickname}: {Ids} ({Text})", nickname, string.Join(",", ids), Truncate(text));

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var awarded = ApplyAwards(nickname, aliases, text, ids);
            if (awarded)
                await SaveUnlockedAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool ApplyAwards(string nickname, IReadOnlyList<string> aliases, string text, IReadOnlyList<string> ids)
    {
        var now = _timeProvider.GetUtcNow();
        var lastMatch = Truncate(text);
        var changed = false;

        var user = FindOrAddUser(nickname, aliases);

        foreach (var id in ids)
        {
            if (!_catalog.TryGetValue(id, out var definition)) continue;

            var entry = user.Achievements.FirstOrDefault(a =>
                string.Equals(a.Id, definition.Id, StringComparison.OrdinalIgnoreCase));
            if (entry == null)
            {
                entry = new AchievementEntryProgress
                {
                    Id = definition.Id,
                    Goal = definition.Goal
                };
                user.Achievements.Add(entry);
            }

            if (entry.Unlocked) continue;

            entry.Goal = definition.Goal;
            entry.Points++;
            entry.LastMatch = lastMatch;
            entry.LastAtUtc = now;
            if (entry.Points >= entry.Goal)
                entry.Unlocked = true;

            changed = true;
        }

        return changed;
    }

    private void LoadCatalog()
    {
        var path = DataPaths.Get(CatalogFileName);
        if (!File.Exists(path))
        {
            Log.Warning("Каталог ачивок {Path} не найден.", path);
            _catalog = new Dictionary<string, AchievementDefinition>(StringComparer.OrdinalIgnoreCase);
            return;
        }

        try
        {
            var json = File.ReadAllText(path);
            var document = JsonSerializer.Deserialize<AchievementCatalogDocument>(json, JsonOptions);
            var map = new Dictionary<string, AchievementDefinition>(StringComparer.OrdinalIgnoreCase);
            if (document?.Achievements != null)
            {
                foreach (var item in document.Achievements)
                {
                    if (string.IsNullOrWhiteSpace(item.Id) || item.Goal <= 0) continue;
                    map[item.Id.Trim()] = item;
                }
            }

            _catalog = map;
            if (map.Count == 0)
                Log.Warning("Каталог ачивок пуст: {Path}.", path);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Не удалось загрузить каталог ачивок.");
            _catalog = new Dictionary<string, AchievementDefinition>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task LoadProgressUnlockedAsync()
    {
        _users.Clear();
        var path = GetProgressFilePath();
        if (!File.Exists(path)) return;

        try
        {
            var json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
            var document = JsonSerializer.Deserialize<AchievementProgressDocument>(json, JsonOptions);
            if (document?.Users == null) return;

            foreach (var user in document.Users)
            {
                if (string.IsNullOrWhiteSpace(user.Nickname)) continue;
                _users[user.Nickname] = user;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Ошибка при загрузке прогресса ачивок.");
        }
    }

    private async Task SaveUnlockedAsync()
    {
        var path = GetProgressFilePath();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var document = new AchievementProgressDocument {Users = GetProgressSnapshot().ToList()};
        var json = JsonSerializer.Serialize(document, JsonOptions);
        var tempFilePath = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFilePath, json).ConfigureAwait(false);
        await Task.Run(() => File.Move(tempFilePath, path, true)).ConfigureAwait(false);
    }

    private AchievementUserProgress FindOrAddUser(string nickname, IReadOnlyList<string> aliases)
    {
        foreach (var alias in aliases)
        {
            if (string.IsNullOrWhiteSpace(alias)) continue;
            if (!_users.TryGetValue(alias, out var existing)) continue;
            if (!alias.Equals(nickname, StringComparison.OrdinalIgnoreCase))
            {
                _users.TryRemove(alias, out _);
                existing.Nickname = nickname;
                _users[nickname] = existing;
            }

            return existing;
        }

        return _users.GetOrAdd(nickname, key => new AchievementUserProgress {Nickname = key});
    }

    private static string Truncate(string text)
    {
        var trimmed = text.Trim();
        return trimmed.Length <= LastMatchMaxLength ? trimmed : trimmed[..LastMatchMaxLength];
    }

    private static AchievementUserProgress CloneUser(AchievementUserProgress user) =>
        new()
        {
            Nickname = user.Nickname,
            Achievements = user.Achievements.Select(a => new AchievementEntryProgress
            {
                Id = a.Id,
                Points = a.Points,
                Goal = a.Goal,
                Unlocked = a.Unlocked,
                LastMatch = a.LastMatch,
                LastAtUtc = a.LastAtUtc
            }).ToList()
        };
}
