using System.Text.RegularExpressions;

namespace AbsoluteBot.Services.Recap;

/// <summary>
/// Подменяет ники на User_N только в тексте запроса. На диск ничего не пишет.
/// </summary>
public static partial class NickMask
{
    public static string Apply(string text, IEnumerable<string> nicks)
    {
        var ordered = nicks
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(n => n.Length)
            .ToList();
        if (ordered.Count == 0 || string.IsNullOrEmpty(text)) return text;

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < ordered.Count; i++)
            map[ordered[i]] = "User_" + (i + 1);

        foreach (var nick in ordered)
        {
            var label = map[nick];
            text = Regex.Replace(text, $@"(?<![0-9A-Za-zА-Яа-я_])@?{Regex.Escape(nick)}(?![0-9A-Za-zА-Яа-я_])", label,
                RegexOptions.IgnoreCase);
        }

        return text;
    }

    public static string StripLabels(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return UserLabel().Replace(text, "чаттерс");
    }

    [GeneratedRegex(@"User_\d+", RegexOptions.IgnoreCase)]
    private static partial Regex UserLabel();
}