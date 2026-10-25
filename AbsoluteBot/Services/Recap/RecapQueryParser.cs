using System.Globalization;
using System.Text.RegularExpressions;

namespace AbsoluteBot.Services.Recap;

/// <summary>
/// Где смотреть пересказ.
/// </summary>
public enum RecapSource
{
    Stream,
    Telegram,
    Discord
}

/// <summary>
/// Откуда пришла команда.
/// </summary>
public enum RecapPlace
{
    StreamChat,
    Telegram,
    Discord
}

/// <summary>
/// Какой кусок истории открыть.
/// </summary>
public abstract record RecapWindow;

/// <summary>Идущий эфир или последний завершённый.</summary>
public sealed record CurrentStreamWindow : RecapWindow;

/// <summary>На сколько эфиров назад от текущего или последнего. 1 — прошлый.</summary>
public sealed record StreamBackWindow(int Steps) : RecapWindow;

/// <summary>Одни календарные сутки по Europe/Vienna.</summary>
public sealed record CalendarDayWindow(DateOnly Day) : RecapWindow;

/// <summary>Интервал от и до, включительно по началу.</summary>
public sealed record RangeWindow(DateTimeOffset From, DateTimeOffset To) : RecapWindow;

/// <summary>С прошлой реплики автора в этом чате.</summary>
public sealed record SinceAuthorWindow : RecapWindow;

/// <summary>
/// Результат разбора хвоста !чтобыло.
/// </summary>
public sealed record RecapParseResult(RecapSource Source, RecapWindow? Window, string? Error)
{
    public bool Ok => Error == null && Window != null;
}

/// <summary>
/// Разбирает свободный русский хвост команды !чтобыло.
/// </summary>
public static partial class RecapQueryParser
{
    public const string Hint =
        "Не понял срок. Например: !чтобыло, !чтобыло вчера, !чтобыло 3 дня назад, !чтобыло за неделю, !чтобыло на прошлом, !чтобыло со вчера.";

    private static readonly Dictionary<string, int> NumberWords = new(StringComparer.Ordinal)
    {
        ["один"] = 1, ["одна"] = 1, ["одну"] = 1, ["пару"] = 2,
        ["два"] = 2, ["две"] = 2, ["двое"] = 2, ["три"] = 3, ["четыре"] = 4, ["пять"] = 5,
        ["шесть"] = 6, ["семь"] = 7, ["восемь"] = 8, ["девять"] = 9, ["десять"] = 10,
        ["одиннадцать"] = 11, ["двенадцать"] = 12, ["тринадцать"] = 13, ["четырнадцать"] = 14,
        ["пятнадцать"] = 15, ["шестнадцать"] = 16, ["семнадцать"] = 17, ["восемнадцать"] = 18,
        ["девятнадцать"] = 19, ["двадцать"] = 20, ["тридцать"] = 30
    };

    private static readonly Dictionary<string, DayOfWeek> Weekdays = new(StringComparer.Ordinal)
    {
        ["понедельник"] = DayOfWeek.Monday, ["понедельника"] = DayOfWeek.Monday,
        ["вторник"] = DayOfWeek.Tuesday, ["вторника"] = DayOfWeek.Tuesday,
        ["среду"] = DayOfWeek.Wednesday, ["среды"] = DayOfWeek.Wednesday, ["среда"] = DayOfWeek.Wednesday,
        ["четверг"] = DayOfWeek.Thursday, ["четверга"] = DayOfWeek.Thursday,
        ["пятницу"] = DayOfWeek.Friday, ["пятницы"] = DayOfWeek.Friday, ["пятница"] = DayOfWeek.Friday,
        ["субботу"] = DayOfWeek.Saturday, ["субботы"] = DayOfWeek.Saturday, ["суббота"] = DayOfWeek.Saturday,
        ["воскресенье"] = DayOfWeek.Sunday, ["воскресенья"] = DayOfWeek.Sunday
    };

    public static RecapParseResult Parse(string? tail, RecapPlace place, DateTimeOffset now)
    {
        var text = Normalize(tail);
        if (text.Length == 0)
            return Default(place);

        if (!TryTakeSource(text, out var explicitSource, out var rest, out var sourceError))
            return new RecapParseResult(RecapSource.Stream, null, sourceError);

        rest = rest.Trim();
        var source = explicitSource ?? DefaultSource(place);
        if (rest.Length == 0)
            return explicitSource == RecapSource.Stream
                ? new RecapParseResult(RecapSource.Stream, new CurrentStreamWindow(), null)
                : new RecapParseResult(source, DefaultWindow(place, explicitSource), null);

        var window = ParseWindow(rest, now);
        if (window == null)
            return new RecapParseResult(source, null, Hint);

        if (explicitSource == null && window is CurrentStreamWindow or StreamBackWindow)
            source = RecapSource.Stream;

        return new RecapParseResult(source, window, null);
    }

    [GeneratedRegex(@"^(за всё время|за все время|за всю историю|вообще)$")]
    private static partial Regex AllTime();

    [GeneratedRegex(@"\s+")]
    private static partial Regex CollapseSpace();

    [GeneratedRegex(@"^(этот стрим|текущий( стрим| эфир)?|сейчас|сегодняшний стрим)$")]
    private static partial Regex CurrentStream();

    [GeneratedRegex(@"^(?:(\d+) )?(?:дня|дней|суток) назад$|^день назад$|^сутки назад$")]
    private static partial Regex DaysAgo();

    private static RecapParseResult Default(RecapPlace place)
    {
        return new RecapParseResult(DefaultSource(place), DefaultWindow(place, null), null);
    }

    private static RecapSource DefaultSource(RecapPlace place)
    {
        return place switch
        {
            RecapPlace.Telegram => RecapSource.Telegram,
            RecapPlace.Discord => RecapSource.Discord,
            _ => RecapSource.Stream
        };
    }

    private static RecapWindow DefaultWindow(RecapPlace place, RecapSource? explicitSource)
    {
        if (explicitSource == RecapSource.Stream || place == RecapPlace.StreamChat)
            return new CurrentStreamWindow();
        return new SinceAuthorWindow();
    }

    [GeneratedRegex(@"\bв дискорде\b")]
    private static partial Regex DiscordSource();

    [GeneratedRegex(@"^с (понедельника|вторника|среды|четверга|пятницы|субботы|воскресенья)$")]
    private static partial Regex FromWeekday();

    [GeneratedRegex(@"^(за полгода|за пол года)$")]
    private static partial Regex HalfYear();

    private static DateOnly LatestOnOrBefore(DateOnly day, DayOfWeek wanted)
    {
        var diff = ((int) day.DayOfWeek - (int) wanted + 7) % 7;
        return day.AddDays(-diff);
    }

    private static string Normalize(string? tail)
    {
        if (string.IsNullOrWhiteSpace(tail)) return string.Empty;
        var text = tail.Trim().ToLowerInvariant().Replace('ё', 'е');
        text = CollapseSpace().Replace(text, " ");
        foreach (var pair in NumberWords.OrderByDescending(p => p.Key.Length))
            text = Regex.Replace(text, $@"(?<=^|\s){pair.Key}(?=\s|$)", pair.Value.ToString(CultureInfo.InvariantCulture));
        return CollapseSpace().Replace(text, " ").Trim();
    }

    private static RecapWindow? ParseWindow(string text, DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(now.DateTime);

        if (SinceAuthor().IsMatch(text)) return new SinceAuthorWindow();
        if (AllTime().IsMatch(text) || HalfYear().IsMatch(text))
            return new RangeWindow(now.AddDays(-183), now);
        if (TryStreamBack(text, out var steps)) return new StreamBackWindow(steps);
        if (PreviousStream().IsMatch(text)) return new StreamBackWindow(1);
        if (CurrentStream().IsMatch(text)) return new CurrentStreamWindow();
        if (SinceYesterday().IsMatch(text))
            return new RangeWindow(RecapClock.StartOfDay(today.AddDays(-1)), now);
        if (SinceToday().IsMatch(text))
            return new RangeWindow(RecapClock.StartOfDay(today), now);
        if (text is "с начала недели")
            return new RangeWindow(RecapClock.StartOfDay(StartOfWeek(today)), now);
        if (text is "с прошлой недели")
            return new RangeWindow(RecapClock.StartOfDay(StartOfWeek(today).AddDays(-7)), now);
        if (text is "с начала месяца" or "с первого числа")
            return new RangeWindow(RecapClock.StartOfDay(new DateOnly(today.Year, today.Month, 1)), now);
        if (FromWeekday().Match(text) is {Success: true} fromDay && Weekdays.TryGetValue(fromDay.Groups[1].Value, out var fromDow))
            return new RangeWindow(RecapClock.StartOfDay(LatestOnOrBefore(today, fromDow)), now);
        if (text is "за эту неделю" or "на этой неделе")
            return new RangeWindow(RecapClock.StartOfDay(StartOfWeek(today)), now);
        if (text is "за прошлую неделю" or "на прошлой неделе")
        {
            var start = StartOfWeek(today).AddDays(-7);
            return new RangeWindow(RecapClock.StartOfDay(start), RecapClock.StartOfDay(start.AddDays(7)));
        }

        if (text is "за этот месяц" or "в этом месяце")
            return new RangeWindow(RecapClock.StartOfDay(new DateOnly(today.Year, today.Month, 1)), now);
        if (text is "за прошлый месяц" or "в прошлом месяце")
        {
            var firstThis = new DateOnly(today.Year, today.Month, 1);
            var firstPrev = firstThis.AddMonths(-1);
            return new RangeWindow(RecapClock.StartOfDay(firstPrev), RecapClock.StartOfDay(firstThis));
        }

        if (text is "за прошлые выходные")
            return Weekend(today, true, now);
        if (text is "за выходные" or "за эти выходные")
            return Weekend(today, false, now);

        var weeks = RollingWeeks().Match(text);
        if (weeks.Success && int.TryParse(weeks.Groups[1].Value, out var weekCount) && weekCount is > 0 and <= 26)
            return new RangeWindow(now.AddDays(-7 * weekCount), now);

        if (text is "за неделю") return new RangeWindow(now.AddDays(-7), now);
        if (text is "за месяц") return new RangeWindow(now.AddDays(-30), now);
        if (text is "за день" or "за сутки") return new RangeWindow(now.AddDays(-1), now);

        var days = RollingDays().Match(text);
        if (days.Success && int.TryParse(days.Groups[1].Value, out var dayCount) && dayCount is > 0 and <= 183)
            return new RangeWindow(now.AddDays(-dayCount), now);

        if (WeekdayMention().Match(text) is {Success: true} weekday &&
            Weekdays.TryGetValue(weekday.Groups[2].Value, out var dow))
        {
            var which = weekday.Groups[1].Value;
            var day = which.StartsWith("прошл", StringComparison.Ordinal)
                ? PreviousWeekday(today, dow)
                : which.StartsWith("эт", StringComparison.Ordinal)
                    ? WeekdayOfThisWeek(today, dow)
                    : LatestOnOrBefore(today, dow);
            return new CalendarDayWindow(day);
        }

        if (text is "позавчера" or "третьего дня" or "за позавчера")
            return new CalendarDayWindow(today.AddDays(-2));
        if (text is "вчера" or "за вчера" or "вчерашний" or "вчерашний день" or "за вчерашний день")
            return new CalendarDayWindow(today.AddDays(-1));
        if (text is "сегодня" or "за сегодня" or "за сегодняшний день" or "сегодняшний")
            return new CalendarDayWindow(today);

        var ago = DaysAgo().Match(text);
        if (ago.Success)
        {
            var n = ago.Groups[1].Success && int.TryParse(ago.Groups[1].Value, out var parsed) ? parsed : 1;
            if (n is < 1 or > 183) return null;
            return new CalendarDayWindow(today.AddDays(-n));
        }

        return null;
    }

    [GeneratedRegex(
        @"^(прошлый( стрим| эфир| трансляция)?|прошлом|прошлого|на прошлом( стриме| эфире| трансляции)?|предыдущий( стрим| эфир| трансляция)?|предыдущем( стриме| эфире)?)$")]
    private static partial Regex PreviousStream();

    private static DateOnly PreviousWeekday(DateOnly day, DayOfWeek wanted)
    {
        var latest = LatestOnOrBefore(day, wanted);
        return latest == day ? latest.AddDays(-7) : latest;
    }

    [GeneratedRegex(@"^за (\d+) (?:дня|дней|суток)$")]
    private static partial Regex RollingDays();

    [GeneratedRegex(@"^за (\d+) недел(?:ю|и|ь)$")]
    private static partial Regex RollingWeeks();

    [GeneratedRegex(@"^(с последнего сообщения|с моего последнего сообщения|с моего сообщения|после меня|с моей последней реплики)$")]
    private static partial Regex SinceAuthor();

    [GeneratedRegex(@"^(с сегодня|с сегодняшнего дня)$")]
    private static partial Regex SinceToday();

    [GeneratedRegex(@"^(со вчера|с вчерашнего дня|с вчера)$")]
    private static partial Regex SinceYesterday();

    private static DateOnly StartOfWeek(DateOnly day)
    {
        var diff = ((int) day.DayOfWeek - (int) DayOfWeek.Monday + 7) % 7;
        return day.AddDays(-diff);
    }

    [GeneratedRegex(@"^(\d+) стрима? назад$|^(\d+) стримов назад$")]
    private static partial Regex StreamsAgo();

    [GeneratedRegex(@"\bна стриме\b|\bна эфире\b|\bна трансляции\b|\bсо стрима\b|\bс эфира\b")]
    private static partial Regex StreamSource();

    [GeneratedRegex(@"\bв телеграме\b|\bв телеграмме\b|\bв телеге\b")]
    private static partial Regex TelegramSource();

    private static bool TryStreamBack(string text, out int steps)
    {
        steps = 0;
        if (text is "позапрошлый" or "позапрошлый стрим" or "позапрошлый эфир" or "позапрошлая трансляция" or "предпоследний" or "предпоследний стрим"
            or "предпоследний эфир")
        {
            steps = 2;
            return true;
        }

        var match = StreamsAgo().Match(text);
        var raw = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
        if (!match.Success || !int.TryParse(raw, out steps)) return false;
        return steps is >= 1 and <= 40;
    }

    private static bool TryTakeSource(string text, out RecapSource? source, out string rest, out string? error)
    {
        source = null;
        error = null;
        rest = text;
        var hits = 0;

        if (TelegramSource().IsMatch(text))
        {
            hits++;
            source = RecapSource.Telegram;
            rest = TelegramSource().Replace(rest, " ");
        }

        if (DiscordSource().IsMatch(text))
        {
            hits++;
            source = RecapSource.Discord;
            rest = DiscordSource().Replace(rest, " ");
        }

        if (StreamSource().IsMatch(text))
        {
            hits++;
            source = RecapSource.Stream;
            rest = StreamSource().Replace(rest, " ");
        }

        rest = CollapseSpace().Replace(rest, " ").Trim();
        if (hits > 1)
        {
            error = Hint;
            return false;
        }

        return true;
    }

    [GeneratedRegex(@"^(?:в|за) (прошлую |прошлый |эту |этот |это )?((?:понедельник|вторник|среду|четверг|пятницу|субботу|воскресенье))$")]
    private static partial Regex WeekdayMention();

    private static DateOnly WeekdayOfThisWeek(DateOnly day, DayOfWeek wanted)
    {
        var monday = StartOfWeek(day);
        var diff = ((int) wanted - (int) DayOfWeek.Monday + 7) % 7;
        return monday.AddDays(diff);
    }

    private static RangeWindow Weekend(DateOnly today, bool previous, DateTimeOffset now)
    {
        var saturday = LatestOnOrBefore(today, DayOfWeek.Saturday);
        if (previous) saturday = saturday.AddDays(-7);
        var from = RecapClock.StartOfDay(saturday);
        var thisWeekend = !previous && today.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        var to = thisWeekend ? now : RecapClock.StartOfDay(saturday.AddDays(2));
        return new RangeWindow(from, to);
    }
}

/// <summary>
/// Календарь пересказов: Europe/Vienna, как у контейнера.
/// </summary>
public static class RecapClock
{
    public static DateTimeOffset Now => TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Zone);
    public static TimeZoneInfo Zone { get; } = Load();

    public static DateTimeOffset StartOfDay(DateOnly day)
    {
        var local = day.ToDateTime(TimeOnly.MinValue);
        var offset = Zone.GetUtcOffset(DateTime.SpecifyKind(local, DateTimeKind.Unspecified));
        return new DateTimeOffset(local, offset);
    }

    public static DateOnly Today(DateTimeOffset moment)
    {
        var local = TimeZoneInfo.ConvertTime(moment, Zone);
        return DateOnly.FromDateTime(local.DateTime);
    }

    private static TimeZoneInfo Load()
    {
        foreach (var id in new[] {"Europe/Vienna", "W. Europe Standard Time"})
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }

        return TimeZoneInfo.Utc;
    }
}