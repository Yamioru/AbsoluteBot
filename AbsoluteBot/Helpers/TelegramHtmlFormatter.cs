using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace AbsoluteBot.Helpers;

/// <summary>
///     Превращает вольный Markdown от нейросети в HTML Telegram и безопасно обрезает, закрывая теги.
/// </summary>
public static partial class TelegramHtmlFormatter
{
    public static string Compose(string title, string body, int maxVisibleLength)
    {
        var htmlTitle = "<b>" + WebUtility.HtmlEncode(title.Trim()) + "</b>";
        var htmlBody = ToHtml(body, maxVisibleLength);
        var combined = string.IsNullOrWhiteSpace(htmlBody) ? htmlTitle : htmlTitle + "\n\n" + htmlBody;
        return TruncateHtml(combined, maxVisibleLength);
    }

    public static string ToHtml(string markdown, int maxVisibleLength)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return string.Empty;

        var text = markdown.Replace("\r\n", "\n").Trim();
        text = HorizontalRuleRegex().Replace(text, "\n");
        text = HeadingRegex().Replace(text, "<b>$1</b>");
        text = BoldStarRegex().Replace(text, "<b>$1</b>");
        text = BoldUnderscoreRegex().Replace(text, "<b>$1</b>");
        text = InlineCodeRegex().Replace(text, "<code>$1</code>");
        text = LinkRegex().Replace(text, m =>
            $"<a href=\"{WebUtility.HtmlEncode(m.Groups[2].Value)}\">{m.Groups[1].Value}</a>");
        text = SingleStarPairRegex().Replace(text, "<b>$1</b>");
        text = ItalicUnderscoreRegex().Replace(text, "<i>$1</i>");
        text = ExtraStarsRegex().Replace(text, string.Empty);
        text = ExtraNewlinesRegex().Replace(text, "\n\n").Trim();
        text = EscapeOutsideTags(text);
        return TruncateHtml(text, maxVisibleLength);
    }

    /// <summary>
    ///     Есть ли целая Markdown-разметка (парные <c>**</c> / <c>`</c>, заголовки, *жирный*, _курсив_).
    /// </summary>
    public static bool HasIntactMarkup(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (CountToken(text, "**") % 2 != 0) return false;
        if (text.Count(c => c == '`') % 2 != 0) return false;

        return BoldStarRegex().IsMatch(text)
               || BoldUnderscoreRegex().IsMatch(text)
               || HeadingRegex().IsMatch(text)
               || InlineCodeRegex().IsMatch(text)
               || LinkRegex().IsMatch(text)
               || SingleStarPairRegex().IsMatch(text)
               || ItalicUnderscoreRegex().IsMatch(text);
    }

    public static bool TryFormat(string message, int maxVisibleLength, out string html)
    {
        html = string.Empty;
        if (!HasIntactMarkup(message)) return false;
        html = ToHtml(message, maxVisibleLength);
        return html.Contains('<');
    }

    internal static string TruncateHtml(string html, int maxVisibleLength)
    {
        if (string.IsNullOrEmpty(html) || maxVisibleLength <= 0) return string.Empty;
        var open = new Stack<string>();
        var builder = new StringBuilder(html.Length);
        var visible = 0;
        var index = 0;

        while (index < html.Length && visible < maxVisibleLength)
        {
            if (html[index] == '<')
            {
                var end = html.IndexOf('>', index);
                if (end < 0) break;
                var tag = html[index..(end + 1)];
                builder.Append(tag);
                ApplyTagToStack(open, tag);
                index = end + 1;
                continue;
            }

            if (html[index] == '&')
            {
                var end = html.IndexOf(';', index);
                if (end < 0 || end - index > 10)
                {
                    builder.Append(html[index]);
                    visible++;
                    index++;
                    continue;
                }

                builder.Append(html[index..(end + 1)]);
                visible++;
                index = end + 1;
                continue;
            }

            builder.Append(html[index]);
            visible++;
            index++;
        }

        while (open.Count > 0)
            builder.Append("</").Append(open.Pop()).Append('>');

        return builder.ToString().Trim();
    }

    private static void ApplyTagToStack(Stack<string> open, string tag)
    {
        if (tag.StartsWith("</", StringComparison.Ordinal))
        {
            var name = TagName(tag[2..]);
            if (open.Count > 0 && string.Equals(open.Peek(), name, StringComparison.OrdinalIgnoreCase))
                open.Pop();
            return;
        }

        if (tag.EndsWith("/>", StringComparison.Ordinal) || tag.StartsWith("<br", StringComparison.OrdinalIgnoreCase))
            return;

        open.Push(TagName(tag[1..]));
    }

    private static string TagName(string tag)
    {
        var end = 0;
        while (end < tag.Length && char.IsLetter(tag[end]))
            end++;
        return tag[..end].ToLowerInvariant();
    }

    private static string EscapeOutsideTags(string text)
    {
        var builder = new StringBuilder(text.Length);
        var index = 0;
        while (index < text.Length)
        {
            if (text[index] == '<')
            {
                var end = text.IndexOf('>', index);
                if (end < 0)
                {
                    builder.Append(WebUtility.HtmlEncode(text[index..]));
                    break;
                }

                var tag = text[index..(end + 1)];
                if (IsAllowedTag(tag))
                    builder.Append(tag);
                else
                    builder.Append(WebUtility.HtmlEncode(tag));
                index = end + 1;
                continue;
            }

            var next = text.IndexOf('<', index);
            if (next < 0) next = text.Length;
            builder.Append(WebUtility.HtmlEncode(text[index..next]));
            index = next;
        }

        return builder.ToString();
    }

    private static bool IsAllowedTag(string tag)
    {
        var name = TagName(tag.TrimStart('<', '/'));
        return name is "b" or "i" or "u" or "s" or "code" or "pre" or "a" or "em" or "strong";
    }

    private static int CountToken(string text, string token)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }

        return count;
    }

    [GeneratedRegex(@"^#{1,6}\s+(.+)$", RegexOptions.Multiline)]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"\*\*(.+?)\*\*", RegexOptions.Singleline)]
    private static partial Regex BoldStarRegex();

    [GeneratedRegex(@"__(.+?)__", RegexOptions.Singleline)]
    private static partial Regex BoldUnderscoreRegex();

    [GeneratedRegex(@"`([^`]+)`")]
    private static partial Regex InlineCodeRegex();

    [GeneratedRegex(@"\[([^\]]+)\]\((https?://[^)\s]+)\)")]
    private static partial Regex LinkRegex();

    [GeneratedRegex(@"(?<![A-Za-zА-Яа-яЁё0-9*])\*([^*\n]{1,200})\*(?![A-Za-zА-Яа-яЁё0-9*])")]
    private static partial Regex SingleStarPairRegex();

    [GeneratedRegex(@"(?<![A-Za-zА-Яа-яЁё0-9_])_([^_\n]{1,80})_(?![A-Za-zА-Яа-яЁё0-9_])")]
    private static partial Regex ItalicUnderscoreRegex();

    [GeneratedRegex(@"^\s*(\*{3,}|-{3,}|_{3,})\s*$", RegexOptions.Multiline)]
    private static partial Regex HorizontalRuleRegex();

    [GeneratedRegex(@"\*{2,}")]
    private static partial Regex ExtraStarsRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ExtraNewlinesRegex();
}
