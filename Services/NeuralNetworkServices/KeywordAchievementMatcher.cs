using System.Globalization;
using System.Text.RegularExpressions;
using AbsoluteBot.Models;

namespace AbsoluteBot.Services.NeuralNetworkServices;

/// <summary>
///     Явные лексические совпадения для ачивок. NLI на коротких фразах часто врёт или падает по памяти;
///     ключевые слова засчитываются независимо.
/// </summary>
public static class KeywordAchievementMatcher
{
    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

    private static readonly (string Id, Regex Pattern)[] Rules =
    {
        ("cat_lover", Rx(@"(?<![а-яё])(кот(ик(а|у|ом|е|и|ов)?|ёнка|енка|ята)?|кошк[ауие]|кис+(а|ка|ки|ку)?)(?![а-яё])")),
        ("doggo", Rx(@"(?<![а-яё])(собак[ауие]|п[её]с(ик)?|щен(ок|к[аиу]))(?![а-яё])")),
        ("sailor_mouth", Rx(@"(?<![а-яё])(сук[ауие]|бля([тд]ь|дки?)?|бл[яе]д|ху[йяеюи]|пизд|ёб|еб[алун]|нахуй|нахер|блять)(?![а-яё])")),
        ("weeb", Rx(@"(?<![а-яёa-z])(аниме|манга|ранобэ|вайфу|геншин|genshin|persona)(?![а-яёa-z])")),
        ("rain_man", Rx(@"(?<![а-яё])(дождь|льёт|жара|снег|моро[зс]|погод[аеуы])(?![а-яё])"))
    };

    public static IReadOnlyList<string> Match(string? text, IReadOnlyList<AchievementDefinition> catalog)
    {
        if (string.IsNullOrWhiteSpace(text) || catalog.Count == 0)
            return Array.Empty<string>();

        var normalized = text.ToLower(Ru);
        var allowed = new HashSet<string>(catalog.Select(item => item.Id), StringComparer.OrdinalIgnoreCase);
        var hits = new List<string>();
        foreach (var (id, pattern) in Rules)
        {
            if (!allowed.Contains(id)) continue;
            if (pattern.IsMatch(normalized))
                hits.Add(id);
        }

        return hits;
    }

    private static Regex Rx(string pattern) =>
        new(pattern, RegexOptions.CultureInvariant | RegexOptions.Compiled);
}
