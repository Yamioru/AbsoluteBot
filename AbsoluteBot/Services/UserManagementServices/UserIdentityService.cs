using System.Text.Encodings.Web;
using System.Text.Json;
using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services.UtilityServices;
using Serilog;

namespace AbsoluteBot.Services.UserManagementServices;

/// <summary>
///     Индекс личностей: один человек на платформе определяется id, никнеймы — только отображаемые алиасы.
/// </summary>
public class UserIdentityService : IAsyncInitializable
{
    private static string FilePath => DataPaths.Get("user_identities.json");
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly List<UserIdentityRecord> _records = new();
    private bool _loaded;

    public async Task InitializeAsync()
    {
        await Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            LoadUnlocked();
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>
    ///     Запоминает ник и id из контекста сообщения (для Twitch — ещё и display name).
    /// </summary>
    public Task RememberFromContextAsync(ChatContext context)
    {
        return RememberAsync(context.Username, context.Platform, context.UserId, ExtraNickname(context));
    }

    /// <summary>
    ///     Сохраняет связку ник+id, если id известен. Возвращает все известные ники этой личности.
    /// </summary>
    public async Task<IReadOnlyList<string>> RememberAsync(string? nickname, string? platform, string? userId,
        string? extraNickname = null)
    {
        await Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            LoadUnlocked();
            var changed = ApplyRememberUnlocked(nickname, platform, userId, extraNickname);
            if (changed)
                await SaveUnlockedAsync().ConfigureAwait(false);
            return BuildAliasesUnlocked(nickname, platform, userId, extraNickname);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при обновлении идентичности пользователя {Nickname}.", nickname);
            return FallbackAliases(nickname, extraNickname);
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>
    ///     Ники одной личности: текущий, плюс все ранее виденные по id или по нику.
    /// </summary>
    public IReadOnlyList<string> GetAliases(string? nickname, string? platform = null, string? userId = null,
        string? extraNickname = null)
    {
        Gate.Wait();
        try
        {
            LoadUnlocked();
            return BuildAliasesUnlocked(nickname, platform, userId, extraNickname);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при поиске алиасов пользователя {Nickname}.", nickname);
            return FallbackAliases(nickname, extraNickname);
        }
        finally
        {
            Gate.Release();
        }
    }

    internal IReadOnlyList<UserIdentityRecord> GetSnapshot()
    {
        Gate.Wait();
        try
        {
            LoadUnlocked();
            return _records.Select(Clone).ToList();
        }
        finally
        {
            Gate.Release();
        }
    }

    private bool ApplyRememberUnlocked(string? nickname, string? platform, string? userId, string? extraNickname)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(platform))
            return false;

        var byId = FindByIdUnlocked(platform, userId);
        var byNick = FindByNickUnlocked(nickname);
        var byExtra = FindByNickUnlocked(extraNickname);

        var target = byId ?? byNick ?? byExtra;
        if (target == null)
        {
            target = new UserIdentityRecord();
            _records.Add(target);
        }

        if (byId != null && byNick != null && !ReferenceEquals(byId, byNick))
            MergeUnlocked(byId, byNick);
        if (byId != null && byExtra != null && !ReferenceEquals(byId, byExtra))
            MergeUnlocked(byId, byExtra);
        if (target != byNick && byNick != null && !ReferenceEquals(target, byNick) && byId == null)
            MergeUnlocked(target, byNick);

        var changed = false;
        changed |= AddNicknameUnlocked(target, nickname);
        changed |= AddNicknameUnlocked(target, extraNickname);
        changed |= AddIdUnlocked(target, platform, userId);
        return changed;
    }

    private IReadOnlyList<string> BuildAliasesUnlocked(string? nickname, string? platform, string? userId,
        string? extraNickname)
    {
        var record = FindByIdUnlocked(platform, userId) ?? FindByNickUnlocked(nickname) ?? FindByNickUnlocked(extraNickname);
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddAlias(result, seen, nickname);
        AddAlias(result, seen, extraNickname);
        if (record?.Nicknames == null) return result;
        foreach (var nick in record.Nicknames)
            AddAlias(result, seen, nick);
        return result;
    }

    private UserIdentityRecord? FindByIdUnlocked(string? platform, string? userId)
    {
        if (string.IsNullOrWhiteSpace(platform) || string.IsNullOrWhiteSpace(userId)) return null;
        return _records.FirstOrDefault(r =>
            r.Ids.TryGetValue(platform, out var id) &&
            string.Equals(id, userId, StringComparison.OrdinalIgnoreCase));
    }

    private UserIdentityRecord? FindByNickUnlocked(string? nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname)) return null;
        return _records.FirstOrDefault(r =>
            r.Nicknames.Any(n => n.Equals(nickname, StringComparison.OrdinalIgnoreCase)));
    }

    private void MergeUnlocked(UserIdentityRecord target, UserIdentityRecord other)
    {
        if (ReferenceEquals(target, other)) return;
        foreach (var nick in other.Nicknames)
            AddNicknameUnlocked(target, nick);
        foreach (var pair in other.Ids)
        {
            if (!target.Ids.ContainsKey(pair.Key))
                target.Ids[pair.Key] = pair.Value;
        }

        _records.Remove(other);
    }

    private static bool AddNicknameUnlocked(UserIdentityRecord record, string? nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname)) return false;
        var nick = nickname.Trim();
        if (record.Nicknames.Any(n => n.Equals(nick, StringComparison.OrdinalIgnoreCase)))
            return false;
        record.Nicknames.Add(nick);
        return true;
    }

    private static bool AddIdUnlocked(UserIdentityRecord record, string platform, string userId)
    {
        if (record.Ids.TryGetValue(platform, out var existing) &&
            string.Equals(existing, userId, StringComparison.OrdinalIgnoreCase))
            return false;
        record.Ids[platform] = userId.Trim();
        return true;
    }

    private void LoadUnlocked()
    {
        if (_loaded) return;
        _loaded = true;
        _records.Clear();
        try
        {
            if (!File.Exists(FilePath)) return;
            var json = File.ReadAllText(FilePath);
            var loaded = JsonSerializer.Deserialize<List<UserIdentityRecord>>(json, JsonOptions);
            if (loaded == null) return;
            foreach (var record in loaded)
            {
                record.Ids = new Dictionary<string, string>(record.Ids ?? new Dictionary<string, string>(),
                    StringComparer.OrdinalIgnoreCase);
                record.Nicknames ??= new List<string>();
                _records.Add(record);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Ошибка при загрузке user_identities.json.");
        }
    }

    private async Task SaveUnlockedAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_records, JsonOptions);
            var tempFilePath = Path.GetTempFileName();
            await File.WriteAllTextAsync(tempFilePath, json).ConfigureAwait(false);
            await Task.Run(() => File.Move(tempFilePath, FilePath, true)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при сохранении user_identities.json.");
        }
    }

    private static void AddAlias(List<string> result, HashSet<string> seen, string? nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname)) return;
        var nick = nickname.Trim();
        if (seen.Add(nick)) result.Add(nick);
    }

    private static IReadOnlyList<string> FallbackAliases(string? nickname, string? extraNickname)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddAlias(result, seen, nickname);
        AddAlias(result, seen, extraNickname);
        return result;
    }

    private static string? ExtraNickname(ChatContext context) =>
        context is TwitchChatContext twitch ? twitch.DisplayedName : null;

    private static UserIdentityRecord Clone(UserIdentityRecord record) =>
        new()
        {
            Nicknames = record.Nicknames.ToList(),
            Ids = new Dictionary<string, string>(record.Ids, StringComparer.OrdinalIgnoreCase)
        };
}
