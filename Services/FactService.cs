using System.Text.RegularExpressions;
using AbsoluteBot.Helpers;
using Serilog;

namespace AbsoluteBot.Services;

#pragma warning disable IDE0028
/// <summary>
///     Сервис для получения фактов с Википедии и случайных статей.
/// </summary>
public partial class FactService(HttpClient httpClient)
{
    private const string FactUrl =
        "https://ru.wikipedia.org/wiki/%D0%A8%D0%B0%D0%B1%D0%BB%D0%BE%D0%BD:%D0%97%D0%BD%D0%B0%D0%B5%D1%82%D0%B5_%D0%BB%D0%B8_%D0%B2%D1%8B";

    private const string ParagraphCloseTag = "</p>";
    private const string ParagraphTag = "<p>";
    private const int MinParagraphLength = 30;
    private const string RandomArticleUrl = "https://ru.wikipedia.org/wiki/Служебная:Случайная_страница";
    private const int FactsToRemoveCount = 4;
    private const int MaxCacheSize = 100;
    private const string ImageReference = "на иллюстрации";
    private readonly HashSet<string> _usedFacts = new();

    /// <summary>
    ///     Асинхронно получает факт с Википедии. Если все факты уже использованы,
    ///     возвращает случайную статью с Википедии.
    /// </summary>
    /// <returns>Очищенный от HTML-тегов текст факта или статьи, либо null в случае ошибки.</returns>
    public virtual async Task<string?> GetFactAsync()
    {
        try
        {
            var factText = await httpClient.GetStringAsync(FactUrl).ConfigureAwait(false);

            // Поиск всех фактов на странице
            var matchList = HtmlLiRegex().Matches(factText);
            var facts = matchList.Select(match => match.Value).ToList();

            // Удаление нежелательных элементов (служебных данных) из списка
            if (facts.Count <= FactsToRemoveCount) return null;
            facts.RemoveRange(0, FactsToRemoveCount);
            facts.RemoveAt(facts.Count - 1);

            // Удаление фактов, содержащих отсылку к картинке
            facts = facts.Where(fact => !fact.Contains(ImageReference)).ToList();

            // Удаление использованных фактов из списка
            foreach (var usedFact in _usedFacts)
                facts.Remove(usedFact);

            // Если факты закончились, возвращается случайная статья
            if (facts.Count < 1)
                factText = await GetRandomWikipediaArticleAsync().ConfigureAwait(false);
            else
                factText = facts.First();

            // Добавление нового факта в список использованных
            AddToCache(factText);

            // Возвращение текста факта, очищенного от HTML-тегов
            return TextProcessingUtils.CleanHtmlTags(factText);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при получении факта с википедии.");
            return null;
        }
    }

    /// <summary>
    ///     Добавляет URL в кэш, удаляя старые URL, если размер кэша превышает ограничение.
    /// </summary>
    /// <param name="url">URL для добавления в кэш.</param>
    private void AddToCache(string url)
    {
        if (_usedFacts.Count >= MaxCacheSize)
            // Удаление старейшего элемента из кэша
            _usedFacts.Remove(_usedFacts.Last());

        _usedFacts.Add(url);
    }

    /// <summary>
    ///     Асинхронно получает содержимое случайной статьи с Википедии.
    /// </summary>
    /// <returns>Очищенный от HTML-тегов текст случайной статьи.</returns>
    private async Task<string> GetRandomWikipediaArticleAsync()
    {
        var pageContent = await httpClient.GetStringAsync(RandomArticleUrl).ConfigureAwait(false);

        // Поиск начала содержимого статьи
        var contentStartIndex = pageContent.IndexOf("mw-content-text", StringComparison.Ordinal);
        pageContent = pageContent[contentStartIndex..];

        var paragraphSearchStartIndex = 0;
        var currentContent = pageContent;

        // Проход по абзацам статьи до тех пор, пока не будет найден абзац достаточной длины
        do
        {
            pageContent = currentContent;
            contentStartIndex = pageContent.IndexOf(ParagraphTag, paragraphSearchStartIndex, StringComparison.Ordinal);
            if (contentStartIndex != -1) paragraphSearchStartIndex = contentStartIndex + ParagraphTag.Length;
            pageContent = pageContent[(contentStartIndex + ParagraphTag.Length)..];
            contentStartIndex = pageContent.IndexOf(ParagraphCloseTag, StringComparison.Ordinal);
            pageContent = pageContent.Remove(contentStartIndex);

            // Удаление HTML-тегов и нежелательных символов
            pageContent = RemoveHtmlTagsAndNbspRegex().Replace(pageContent, "").Trim();
            pageContent = RemoveParenthesizedTextRegex().Replace(pageContent, "");
        } while (pageContent.Length < MinParagraphLength);

        return pageContent;
    }

    [GeneratedRegex("<li>.*<\\/li>")]
    private static partial Regex HtmlLiRegex();

    [GeneratedRegex("<[^>]+>|&nbsp;")]
    private static partial Regex RemoveHtmlTagsAndNbspRegex();

    [GeneratedRegex(@"\([^)]+\)\.")]
    private static partial Regex RemoveParenthesizedTextRegex();
}