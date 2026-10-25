using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AbsoluteBot.Services.UtilityServices;
using Serilog;

namespace AbsoluteBot.Services.Recap;

/// <summary>
/// Журнал реплик с настоящими никами и сегменты игр на стриме. В нейросеть этот тип сам ничего не отправляет.
/// </summary>
public sealed partial class ChatJournalService(ConfigService configService, ICensorshipService censorshipService) : IAsyncInitializable
{
    public const int RetentionDays = 183;
    private const int MaxStoredLength = 2000;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private HashSet<string> _botNames = new(StringComparer.OrdinalIgnoreCase);
    private int? _openStreamNumber;
    private string _root = DataPaths.Get("chat_journal");

    public async Task InitializeAsync()
    {
        try
        {
            var names = new[]
            {
                await configService.GetConfigValueAsync<string>("BotName").ConfigureAwait(false),
                await configService.GetConfigValueAsync<string>("TwitchBotName").ConfigureAwait(false),
                await configService.GetConfigValueAsync<string>("VkPlayBotName").ConfigureAwait(false),
                await configService.GetConfigValueAsync<string>("TelegramBotName").ConfigureAwait(false),
                await configService.GetConfigValueAsync<string>("DiscordBotName").ConfigureAwait(false)
            };
            _botNames = names.Where(n => !string.IsNullOrWhiteSpace(n)).ToHashSet(StringComparer.OrdinalIgnoreCase)!;
            await PruneAsync(DateTime.UtcNow).ConfigureAwait(false);
            var sessions = await ReadSessionsAsync().ConfigureAwait(false);
            _openStreamNumber = sessions.LastOrDefault(s => s.EndedUtc == null)?.Number;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Не удалось подготовить журнал чата.");
        }
    }

    public void BeginStream(int number, string? gameName)
    {
        UpdateSessions(sessions =>
        {
            CloseOpen(sessions, DateTime.UtcNow);
            sessions.Add(Open(number, gameName, DateTime.UtcNow));
            _openStreamNumber = number;
        });
    }

    public void ChangeGame(string? gameName)
    {
        if (string.IsNullOrWhiteSpace(gameName)) return;
        UpdateSessions(sessions =>
        {
            var open = sessions.LastOrDefault(s => s.EndedUtc == null);
            if (open == null) return;
            var now = DateTime.UtcNow;
            var current = open.Games.LastOrDefault();
            if (current != null && current.Name.Equals(gameName, StringComparison.OrdinalIgnoreCase) && current.ToUtc == null)
                return;
            if (current is {ToUtc: null}) current.ToUtc = now;
            open.Games.Add(new GameSegment {Name = gameName.Trim(), FromUtc = now});
            _openStreamNumber = open.Number;
        });
    }

    public void EndStream()
    {
        UpdateSessions(sessions =>
        {
            CloseOpen(sessions, DateTime.UtcNow);
            _openStreamNumber = null;
        });
    }

    public void EnsureOpen(int number, string? gameName)
    {
        UpdateSessions(sessions =>
        {
            if (sessions.Any(s => s.Number == number && s.EndedUtc == null))
            {
                _openStreamNumber = number;
                return;
            }

            CloseOpen(sessions, DateTime.UtcNow);
            sessions.Add(Open(number, gameName, DateTime.UtcNow));
            _openStreamNumber = number;
        });
    }

    public async Task<JournalLine?> LastByUserAsync(RecapSource source, string userId, DateTime? notAfterUtc = null)
    {
        var rows = await ReadAsync(source, DateTime.UnixEpoch, DateTime.UtcNow.AddMinutes(1), null).ConfigureAwait(false);
        for (var i = rows.Count - 1; i >= 0; i--)
        {
            var row = rows[i];
            if (!string.Equals(row.UserId, userId, StringComparison.OrdinalIgnoreCase)) continue;
            if (notAfterUtc != null && row.AtUtc >= notAfterUtc.Value) continue;
            return row;
        }

        return null;
    }

    public async Task PruneAsync(DateTime utcNow)
    {
        var cutoff = utcNow.AddDays(-RetentionDays);
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!Directory.Exists(_root)) return;
            foreach (var source in new[] {RecapSource.Stream, RecapSource.Telegram, RecapSource.Discord})
            {
                var path = PathFor(source);
                if (!File.Exists(path)) continue;
                var kept = new List<string>();
                foreach (var line in await File.ReadAllLinesAsync(path).ConfigureAwait(false))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        var entry = JsonSerializer.Deserialize<JournalLine>(line, JsonOptions);
                        if (entry != null && entry.AtUtc >= cutoff) kept.Add(line);
                    }
                    catch (JsonException)
                    {
                    }
                }

                await File.WriteAllLinesAsync(path, kept).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<JournalLine>> ReadAsync(RecapSource source, DateTime fromUtc, DateTime toUtc, int? streamNumber)
    {
        var path = PathFor(source);
        if (!File.Exists(path)) return Array.Empty<JournalLine>();
        var list = new List<JournalLine>();
        foreach (var line in await File.ReadAllLinesAsync(path).ConfigureAwait(false))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            JournalLine? entry;
            try
            {
                entry = JsonSerializer.Deserialize<JournalLine>(line, JsonOptions);
            }
            catch (JsonException)
            {
                continue;
            }

            if (entry == null) continue;
            if (streamNumber != null)
            {
                if (entry.Stream != streamNumber) continue;
            }
            else if (entry.AtUtc < fromUtc || entry.AtUtc >= toUtc)
            {
                continue;
            }

            list.Add(entry);
        }

        return list;
    }

    public async Task<string?> ReadDigestAsync(RecapSource source, DateOnly day)
    {
        var path = DigestPath(source, day);
        return File.Exists(path) ? await File.ReadAllTextAsync(path).ConfigureAwait(false) : null;
    }

    public async Task<IReadOnlyList<StreamSession>> ReadSessionsAsync()
    {
        var path = Path.Combine(_root, "sessions.json");
        if (!File.Exists(path)) return Array.Empty<StreamSession>();
        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<List<StreamSession>>(stream, JsonOptions).ConfigureAwait(false)
                   ?? new List<StreamSession>();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Журнал сегментов стрима не прочитался.");
            return Array.Empty<StreamSession>();
        }
    }

    public void Record(RecapSource source, string? nickname, string? userId, string? text)
    {
        if (string.IsNullOrWhiteSpace(nickname) || string.IsNullOrWhiteSpace(text)) return;
        if (_botNames.Contains(nickname)) return;
        if (text.StartsWith("[Twitch]", StringComparison.Ordinal)) return;

        var clean = Scrub(censorshipService.ApplyCensorship(text, MaxStoredLength, false));
        if (string.IsNullOrWhiteSpace(clean)) return;

        var entry = new JournalLine
        {
            AtUtc = DateTime.UtcNow,
            Nick = nickname.Trim(),
            UserId = string.IsNullOrWhiteSpace(userId) ? nickname.Trim() : userId.Trim(),
            Text = clean,
            Stream = source == RecapSource.Stream ? _openStreamNumber : null
        };

        var taken = false;
        try
        {
            _gate.Wait();
            taken = true;
            Directory.CreateDirectory(_root);
            File.AppendAllText(PathFor(source), JsonSerializer.Serialize(entry, JsonOptions) + Environment.NewLine);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Не удалось записать реплику в журнал чата.");
        }
        finally
        {
            if (taken) _gate.Release();
        }
    }

    public static string Scrub(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        text = Email().Replace(text, "[почта]");
        text = Phone().Replace(text, "[телефон]");
        text = LongNumber().Replace(text, "[номер]");
        text = UrlQuery().Replace(text, "$1");
        return text.Trim();
    }

    /// <summary>Каталог журнала. В тестах подменяется до записи.</summary>
    public void UseDirectory(string path)
    {
        _root = path;
    }

    public async Task WriteDigestAsync(RecapSource source, DateOnly day, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var path = DigestPath(source, day);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, text.Trim()).ConfigureAwait(false);
    }

    private static void CloseOpen(List<StreamSession> sessions, DateTime now)
    {
        var open = sessions.LastOrDefault(s => s.EndedUtc == null);
        if (open == null) return;
        open.EndedUtc = now;
        var segment = open.Games.LastOrDefault();
        if (segment is {ToUtc: null}) segment.ToUtc = now;
    }

    private string DigestPath(RecapSource source, DateOnly day)
    {
        return Path.Combine(_root, "digests", source.ToString().ToLowerInvariant(),
            day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".txt");
    }

    [GeneratedRegex(@"[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase)]
    private static partial Regex Email();

    [GeneratedRegex(@"\d{12,}")]
    private static partial Regex LongNumber();

    private static StreamSession Open(int number, string? gameName, DateTime now)
    {
        return new StreamSession
        {
            Number = number,
            StartedUtc = now,
            Games = string.IsNullOrWhiteSpace(gameName)
                ? new List<GameSegment>()
                : new List<GameSegment> {new() {Name = gameName.Trim(), FromUtc = now}}
        };
    }

    private string PathFor(RecapSource source)
    {
        return Path.Combine(_root, source switch
        {
            RecapSource.Telegram => "telegram.jsonl",
            RecapSource.Discord => "discord.jsonl",
            _ => "stream.jsonl"
        });
    }

    [GeneratedRegex(@"(?<!\d)(?:\+?\d[\d\-\s()]{8,}\d)")]
    private static partial Regex Phone();

    private void UpdateSessions(Action<List<StreamSession>> change)
    {
        var taken = false;
        try
        {
            _gate.Wait();
            taken = true;
            Directory.CreateDirectory(_root);
            var path = Path.Combine(_root, "sessions.json");
            var sessions = File.Exists(path)
                ? JsonSerializer.Deserialize<List<StreamSession>>(File.ReadAllText(path), JsonOptions) ?? new List<StreamSession>()
                : new List<StreamSession>();
            change(sessions);
            File.WriteAllText(path, JsonSerializer.Serialize(sessions, JsonOptions));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Не удалось обновить сегменты стрима.");
        }
        finally
        {
            if (taken) _gate.Release();
        }
    }

    [GeneratedRegex(@"(https?://[^\s?]+)\?[^\s]*", RegexOptions.IgnoreCase)]
    private static partial Regex UrlQuery();
}

public sealed class JournalLine
{
    public DateTime AtUtc { get; set; }
    public int? Stream { get; set; }
    public string Nick { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

public sealed class StreamSession
{
    public DateTime StartedUtc { get; set; }
    public DateTime? EndedUtc { get; set; }
    public int Number { get; set; }
    public List<GameSegment> Games { get; set; } = new();
}

public sealed class GameSegment
{
    public DateTime FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public string Name { get; set; } = string.Empty;
}