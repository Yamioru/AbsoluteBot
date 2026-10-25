using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AbsoluteBot.Helpers;
using AbsoluteBot.Services.GoogleSheetsServices;
using AbsoluteBot.Services.NeuralNetworkServices;
using AbsoluteBot.Services.UtilityServices;

namespace AbsoluteBot.Services.Recap;

/// <summary>
/// Собирает пересказ !чтобыло: игры, реплики и дневные конспекты. Ники подменяются только в запросе к Gemini.
/// </summary>
public sealed class ChatRecapService(
    ChatJournalService journal,
    StreamGoogleSheetsService streamSheets,
    GameProgressService gameProgress,
    GeminiAskClient geminiAsk,
    NeuralModelConfigService modelConfig,
    ICensorshipService censorship)
{
    private const int RawCap = 80;

    public async Task<string> AnswerAsync(RecapParseResult parsed, string userId, int maxLength)
    {
        if (!parsed.Ok || parsed.Window == null)
            return parsed.Error ?? RecapQueryParser.Hint;

        var selection = await SelectAsync(parsed, userId).ConfigureAwait(false);
        if (selection.EmptyNote != null)
            return selection.EmptyNote;

        var liveNow = parsed.Window is CurrentStreamWindow && selection.Live;
        var prefix = liveNow ? string.Empty : selection.DateLine;
        if (selection.TookLastDayBecauseNoOwnMessage)
            prefix = "Своих сообщений ещё не было, беру последние сутки. " + prefix;

        var room = Math.Max(80, maxLength - prefix.Length);
        var body = await NarrateAsync(selection, room).ConfigureAwait(false);
        var answer = (prefix + body).Trim();
        answer = NickMask.StripLabels(answer);
        foreach (var nick in selection.Nicks.OrderByDescending(n => n.Length))
        {
            if (nick.Length < 2) continue;
            answer = Regex.Replace(answer, $@"(?<![0-9A-Za-zА-Яа-я_]){Regex.Escape(nick)}(?![0-9A-Za-zА-Яа-я_])", "зритель",
                RegexOptions.IgnoreCase);
        }

        return censorship.ApplyCensorship(TextProcessingUtils.CutSentence(answer, maxLength), maxLength, false);
    }

    private async Task AppendChatAsync(RecapSource source, StringBuilder facts, IReadOnlyList<JournalLine> lines, bool raw)
    {
        if (lines.Count == 0) return;
        if (!raw)
        {
            foreach (var day in lines.GroupBy(l => RecapClock.Today(new DateTimeOffset(l.AtUtc, TimeSpan.Zero))))
            {
                var digest = await EnsureDigestAsync(source, day.Key, day.ToList()).ConfigureAwait(false);
                facts.Append(day.Key.ToString("dd.MM.yyyy")).Append(": ").AppendLine(digest);
            }

            return;
        }

        facts.AppendLine("Реплики:");
        foreach (var line in lines.TakeLast(RawCap))
            facts.Append(line.Nick).Append(": ").AppendLine(line.Text);
        if (lines.Count > RawCap)
            facts.Append("Показаны последние ").Append(RawCap).Append(" из ").Append(lines.Count).AppendLine(".");
    }

    private static string DateLine(DateTimeOffset from, DateTimeOffset to)
    {
        var culture = CultureInfo.GetCultureInfo("ru-RU");
        var start = TimeZoneInfo.ConvertTime(from, RecapClock.Zone);
        var end = TimeZoneInfo.ConvertTime(to, RecapClock.Zone);
        if (start.Date == end.Date) return start.ToString("d MMMM yyyy", culture) + ". ";
        return start.ToString("d MMMM", culture) + " — " + end.ToString("d MMMM yyyy", culture) + ". ";
    }

    private string DescribeGame(IReadOnlyList<KnownStream> all, KnownStream stream, string game)
    {
        gameProgress.TryGetPlaytime(game, out var minutes, out var percent);
        var occurrence = all.Count(s => s.Number <= stream.Number &&
                                        s.Games.Any(g => g.Equals(game, StringComparison.OrdinalIgnoreCase)));
        var text = game;
        if (occurrence > 0) text += $", уже {occurrence}-й стрим этой игры";
        if (percent >= 0) text += $", прогресс {percent:0.#}%";
        if (minutes > 0) text += $", наиграно {minutes / 60} ч";
        return text;
    }

    private async Task<string> EnsureDigestAsync(RecapSource source, DateOnly day, List<JournalLine> lines)
    {
        var existing = await journal.ReadDigestAsync(source, day).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(existing)) return existing.Trim();

        var body = string.Join('\n', lines.Take(RawCap).Select(l => $"{l.Nick}: {l.Text}"));
        string? summary = null;
        if (geminiAsk.HasApiKeys)
            summary = await NeuralModelAttempts.RunAsync(
                modelConfig.GetGeminiAttemptModels(NeuralEntities.Ask),
                NeuralEntities.Ask,
                model => geminiAsk.AskAsync(body,
                    "Сожми темы этого дня в 3–5 предложений. Ники оставляй как в тексте. Не выдумывай.",
                    null, model, 0.2, false)).ConfigureAwait(false);

        summary = string.IsNullOrWhiteSpace(summary) ? Trim(body, 500) : summary.Trim();
        await journal.WriteDigestAsync(source, day, summary).ConfigureAwait(false);
        return summary;
    }

    private static string Fallback(RecapSelection selection)
    {
        if (selection.GameLines.Count == 0)
            return selection.MessageCount == 0 ? "За этот срок сообщений нет." : $"В чате было {selection.MessageCount} сообщений.";
        return string.Join(", ", selection.GameLines);
    }

    private async Task<RecapSelection> FromMessagesAsync(RecapSource source, IReadOnlyList<JournalLine> lines, bool raw, string dateLine)
    {
        var facts = new StringBuilder();
        await AppendChatAsync(source, facts, lines, raw).ConfigureAwait(false);
        return new RecapSelection
        {
            Facts = facts.ToString(),
            DateLine = dateLine,
            MessageCount = lines.Count,
            Nicks = lines.Select(l => l.Nick).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        };
    }

    private async Task<RecapSelection> FromStreamsAsync(IReadOnlyList<KnownStream> all, IReadOnlyList<KnownStream> picked,
        IReadOnlyList<JournalLine> lines, bool raw)
    {
        var live = picked.Count == 1 && picked[0].EndedUtc == null;
        var culture = CultureInfo.GetCultureInfo("ru-RU");
        var dates = picked.Select(s => s.When.ToString("d MMMM yyyy", culture)).Distinct().ToList();
        var dateLine = dates.Count == 0 ? string.Empty : string.Join(", ", dates) + ". ";
        if (picked.Count > 1)
            dateLine += "Стримы " + string.Join(" и ", picked.Select(s => s.Number)) + ". ";

        var facts = new StringBuilder();
        var gameLines = new List<string>();
        foreach (var stream in picked)
        {
            facts.Append("Стрим ").Append(stream.Number);
            if (stream.When != default) facts.Append(" (").Append(stream.When.ToString("dd.MM.yyyy")).Append(')');
            facts.AppendLine(":");
            if (stream.Games.Count == 0) facts.AppendLine("Игры не записаны.");
            foreach (var game in stream.Games)
            {
                var line = DescribeGame(all, stream, game);
                gameLines.Add(line);
                facts.AppendLine(line);
            }
        }

        await AppendChatAsync(RecapSource.Stream, facts, lines, raw).ConfigureAwait(false);
        return new RecapSelection
        {
            Facts = facts.ToString(),
            DateLine = dateLine,
            Live = live,
            GameLines = gameLines,
            MessageCount = lines.Count,
            Nicks = lines.Select(l => l.Nick).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        };
    }

    private async Task<List<KnownStream>> LoadStreamsAsync()
    {
        var map = new Dictionary<int, KnownStream>();
        foreach (var row in await streamSheets.ListStreamsAsync().ConfigureAwait(false))
            map[row.Number] = new KnownStream
            {
                Number = row.Number,
                When = row.Date,
                Games = row.Games.Where(g => !string.IsNullOrWhiteSpace(g)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                StartedUtc = RecapClock.StartOfDay(row.Date).UtcDateTime,
                EndedUtc = RecapClock.StartOfDay(row.Date.AddDays(1)).UtcDateTime
            };

        foreach (var session in await journal.ReadSessionsAsync().ConfigureAwait(false))
        {
            if (!map.TryGetValue(session.Number, out var known))
            {
                known = new KnownStream {Number = session.Number};
                map[session.Number] = known;
            }

            known.StartedUtc = session.StartedUtc;
            known.EndedUtc = session.EndedUtc;
            known.When = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(session.StartedUtc, RecapClock.Zone));
            var names = session.Games.Select(g => g.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
            if (names.Count > 0) known.Games = names;
        }

        return map.Values.OrderBy(s => s.Number).ToList();
    }

    private async Task<string> NarrateAsync(RecapSelection selection, int maxLength)
    {
        var nicks = selection.Nicks.ToList();
        if (!geminiAsk.HasApiKeys)
            return Fallback(selection);

        var prompt = NickMask.Apply(selection.Facts, nicks);
        var instruction =
            "Перескажи по-русски только то, что есть во входе. User_N — метки, не имена: не произноси их и не выдумывай ники, говори «один из зрителей» или «в чате». " +
            "Подстрой текст под число игр: ноль, одна или несколько. Не добавляй тем, которых нет. Если про чат ничего не передано, расскажи только игры и не пиши, что чат не записан. " +
            $"Уложись в {maxLength} символов. Без markdown.";
        var text = await NeuralModelAttempts.RunAsync(
            modelConfig.GetGeminiAttemptModels(NeuralEntities.Ask),
            NeuralEntities.Ask,
            model => geminiAsk.AskAsync(prompt, instruction, null, model, 0.4, false)).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(text) ? Fallback(selection) : text.Trim();
    }

    private async Task<RecapSelection> SelectAsync(RecapParseResult parsed, string userId)
    {
        var source = parsed.Source;
        var now = DateTime.UtcNow;
        if (source != RecapSource.Stream)
            return await SelectChatAsync(source, parsed.Window!, userId, now).ConfigureAwait(false);

        var known = await LoadStreamsAsync().ConfigureAwait(false);
        return parsed.Window switch
        {
            CurrentStreamWindow => await SelectStreamsAsync(known, known.Count == 0 ? -1 : known.Count - 1, true).ConfigureAwait(false),
            StreamBackWindow back => await SelectStreamsAsync(known, known.Count - 1 - back.Steps, true).ConfigureAwait(false),
            CalendarDayWindow day => await SelectByTime(known, RecapClock.StartOfDay(day.Day), RecapClock.StartOfDay(day.Day.AddDays(1)), true,
                day.Day).ConfigureAwait(false),
            RangeWindow range => await SelectRangeAsync(known, range.From, range.To).ConfigureAwait(false),
            SinceAuthorWindow => await SelectSinceAuthorAsync(RecapSource.Stream, userId, now, known).ConfigureAwait(false),
            _ => new RecapSelection {EmptyNote = RecapQueryParser.Hint}
        };
    }

    private async Task<RecapSelection> SelectByTime(IReadOnlyList<KnownStream> known, DateTimeOffset from, DateTimeOffset to, bool raw,
        DateOnly? missingDay)
    {
        var picked = known.Where(s => s.Overlaps(from, to)).ToList();
        if (picked.Count == 0)
        {
            var when = missingDay?.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) ?? from.ToString("dd.MM.yyyy");
            return new RecapSelection {EmptyNote = $"{when} стрима не было."};
        }

        var lines = new List<JournalLine>();
        foreach (var stream in picked)
            lines.AddRange(await journal.ReadAsync(RecapSource.Stream, DateTime.UnixEpoch, DateTime.UtcNow.AddMinutes(1), stream.Number)
                .ConfigureAwait(false));
        if (lines.Count == 0)
            lines.AddRange(await journal.ReadAsync(RecapSource.Stream, from.UtcDateTime, to.UtcDateTime, null).ConfigureAwait(false));
        return await FromStreamsAsync(known, picked, lines, raw).ConfigureAwait(false);
    }

    private async Task<RecapSelection> SelectChatAsync(RecapSource source, RecapWindow window, string userId, DateTime nowUtc)
    {
        switch (window)
        {
            case SinceAuthorWindow:
                return await SelectSinceAuthorAsync(source, userId, nowUtc, Array.Empty<KnownStream>()).ConfigureAwait(false);
            case CalendarDayWindow day:
            {
                var from = RecapClock.StartOfDay(day.Day);
                var lines = await journal.ReadAsync(source, from.UtcDateTime, RecapClock.StartOfDay(day.Day.AddDays(1)).UtcDateTime, null)
                    .ConfigureAwait(false);
                if (lines.Count == 0)
                    return new RecapSelection {EmptyNote = "За этот срок сообщений нет."};
                return await FromMessagesAsync(source, lines, true, $"{day.Day:dd.MM.yyyy}. ").ConfigureAwait(false);
            }
            case RangeWindow range:
            {
                var lines = await journal.ReadAsync(source, range.From.UtcDateTime, range.To.UtcDateTime, null).ConfigureAwait(false);
                if (lines.Count == 0)
                    return new RecapSelection {EmptyNote = "За этот срок сообщений нет."};
                var longWindow = range.To - range.From > TimeSpan.FromHours(36);
                return await FromMessagesAsync(source, lines, !longWindow, DateLine(range.From, range.To)).ConfigureAwait(false);
            }
            default:
                return new RecapSelection
                    {EmptyNote = "Для этого чата нужен срок, не номер стрима. Например: !чтобыло вчера или !чтобыло за неделю."};
        }
    }

    private async Task<RecapSelection> SelectRangeAsync(IReadOnlyList<KnownStream> known, DateTimeOffset from, DateTimeOffset to)
    {
        var picked = known.Where(s => s.Overlaps(from, to)).ToList();
        var lines = await journal.ReadAsync(RecapSource.Stream, from.UtcDateTime, to.UtcDateTime, null).ConfigureAwait(false);
        if (picked.Count == 0 && lines.Count == 0)
            return new RecapSelection {EmptyNote = "За этот срок стримов не было."};
        var longWindow = to - from > TimeSpan.FromHours(36);
        var selection = await FromStreamsAsync(known, picked, lines, !longWindow).ConfigureAwait(false);
        if (string.IsNullOrEmpty(selection.DateLine))
            selection.DateLine = DateLine(from, to);
        return selection;
    }

    private async Task<RecapSelection> SelectSinceAuthorAsync(RecapSource source, string userId, DateTime nowUtc, IReadOnlyList<KnownStream> known)
    {
        var last = await journal.LastByUserAsync(source, userId).ConfigureAwait(false);
        var from = last?.AtUtc ?? nowUtc.AddDays(-1);
        var note = last == null;
        if (source == RecapSource.Stream)
        {
            var selection = await SelectRangeAsync(known, new DateTimeOffset(DateTime.SpecifyKind(from, DateTimeKind.Utc)), DateTimeOffset.UtcNow)
                .ConfigureAwait(false);
            selection.TookLastDayBecauseNoOwnMessage = note;
            return selection;
        }

        var lines = await journal.ReadAsync(source, from, nowUtc.AddMinutes(1), null).ConfigureAwait(false);
        if (lines.Count == 0)
            return new RecapSelection {EmptyNote = "За этот срок сообщений нет.", TookLastDayBecauseNoOwnMessage = note};
        var longWindow = nowUtc - from > TimeSpan.FromHours(36);
        var selectionChat = await FromMessagesAsync(source, lines, !longWindow, string.Empty).ConfigureAwait(false);
        selectionChat.TookLastDayBecauseNoOwnMessage = note;
        return selectionChat;
    }

    private async Task<RecapSelection> SelectStreamsAsync(IReadOnlyList<KnownStream> known, int index, bool raw)
    {
        if (known.Count == 0)
            return new RecapSelection {EmptyNote = "Стримов ещё не видно."};
        if (index < 0 || index >= known.Count)
            return new RecapSelection {EmptyNote = "Такого стрима нет."};

        var picked = new List<KnownStream> {known[index]};
        var lines = new List<JournalLine>();
        lines.AddRange(await journal.ReadAsync(RecapSource.Stream, DateTime.UnixEpoch, DateTime.UtcNow.AddMinutes(1), picked[0].Number)
            .ConfigureAwait(false));
        return await FromStreamsAsync(known, picked, lines, raw).ConfigureAwait(false);
    }

    private static string Trim(string text, int max)
    {
        return text.Length <= max ? text : text[..max];
    }

    private sealed class RecapSelection
    {
        public bool Live { get; init; }
        public bool TookLastDayBecauseNoOwnMessage { get; set; }
        public int MessageCount { get; init; }
        public List<string> GameLines { get; init; } = new();
        public List<string> Nicks { get; init; } = new();
        public string Facts { get; set; } = string.Empty;
        public string DateLine { get; set; } = string.Empty;
        public string? EmptyNote { get; init; }
    }

    private sealed class KnownStream
    {
        public DateOnly When { get; set; }
        public DateTime StartedUtc { get; set; }
        public DateTime? EndedUtc { get; set; }
        public int Number { get; init; }
        public List<string> Games { get; set; } = new();

        public bool Overlaps(DateTimeOffset from, DateTimeOffset to)
        {
            var start = new DateTimeOffset(DateTime.SpecifyKind(StartedUtc, DateTimeKind.Utc));
            var end = EndedUtc == null ? DateTimeOffset.UtcNow : new DateTimeOffset(DateTime.SpecifyKind(EndedUtc.Value, DateTimeKind.Utc));
            return start < to && end > from;
        }
    }
}