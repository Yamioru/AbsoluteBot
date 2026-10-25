using AbsoluteBot.Helpers;
using HtmlAgilityPack;
using Serilog;

namespace AbsoluteBot.Services.GoogleSearch;

/// <summary>
///     Сервис для поиска определения термина с использованием Google и других источников.
/// </summary>
public class GoogleSearchDefinitionService
    (HttpClient httpClient) : IGoogleSearchDefinitionService
{
    private const string Utf8Encoding = "UTF-8";
    private const string UserAgentString = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/116.0.0.0 Safari/537.36";
    private const string AcceptLanguageHeader = "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7";

    /// <summary>
    ///     Получает определение термина, используя поисковые системы или нейросети.
    /// </summary>
    /// <param name="query">Термин для поиска.</param>
    /// <param name="maxLength">Максимальная длина возвращаемого текста.</param>
    /// <returns>Возвращает определение термина или <c>null</c>, если ничего не найдено.</returns>
    public async Task<string?> GetDefinitionAsync(string query, int maxLength)
    {
        try
        {
            var formattedQuery = FormatQuery(query);
            var response = await FetchSearchResultsAsync(formattedQuery).ConfigureAwait(false);

            var definition = TryExtractDefinitionFromGoogle(response);
            if (!string.IsNullOrEmpty(definition)) return TextProcessingUtils.CleanHtmlTags(definition);
            return string.IsNullOrEmpty(definition) ? null : definition;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при выполнении поиска определения в гугле.");
            return null;
        }
    }
    /// <summary>
    ///     Извлекает определение из узла с атрибутом VisualDigestDescription, игнорируя классы.
    /// </summary>
    /// <param name="document">HTML-документ страницы.</param>
    /// <returns>Возвращает текст определения из визуального описания.</returns>
    private static string? ExtractDefinitionFromVisualDigest(HtmlDocument document)
    {
        // Поиск узла с атрибутом data-attrid="VisualDigestDescription"
        var visualDigestNode = document.DocumentNode
            .SelectSingleNode("//div[@data-attrid='VisualDigestDescription']//span");

        return visualDigestNode?.InnerText.Trim();
    }

    /// <summary>
    ///     Извлекает определение из описания на странице.
    /// </summary>
    /// <param name="document">HTML-документ страницы.</param>
    /// <returns>Возвращает текст определения.</returns>
    private static string? ExtractDefinitionFromDescription(HtmlDocument document)
    {
        // Поиск узла, который содержит текст "Описание"
        var descriptionNode = document.DocumentNode
            .SelectNodes(
                "//*[self::h1 or self::h2 or self::h3 or self::h4 or self::h5 or self::h6][contains(text(),'Описание')]")
            ?.FirstOrDefault();

        // Извлечение текста, следующего за узлом h3
        var descriptionTextNode = descriptionNode?.ParentNode
            .SelectSingleNode(".//span/span");

        return descriptionTextNode?.InnerText;
    }

    /// <summary>
    ///     Извлекает определение из Google Dictionary.
    /// </summary>
    /// <param name="document">HTML-документ страницы.</param>
    /// <returns>Возвращает текст определения из словаря.</returns>
    private static string? ExtractDefinitionFromDictionary(HtmlDocument document)
    {
        // Поиск узла с атрибутом "SenseDefinition"
        var definitionNode = document.DocumentNode
            .SelectSingleNode("//div[@data-attrid='SenseDefinition']");
        return definitionNode?.InnerText.Trim();
    }

    /// <summary>
    ///     Извлекает выделенное описание из интернета.
    /// </summary>
    /// <param name="document">HTML-документ страницы.</param>
    /// <returns>Возвращает текст выделенного описания.</returns>
    private static string? ExtractDefinitionFromHighlightedText(HtmlDocument document)
    {
        // Поиск узла с заголовком "Выделенное описание из Интернета"
        var descriptionHeader = document.DocumentNode
            .SelectSingleNode(
                "//*[self::h1 or self::h2 or self::h3 or self::h4 or self::h5 or self::h6][contains(text(),'Выделенное описание из Интернета')]");

        // Поиск следующего элемента div, который содержит описание
        var descriptionNode = descriptionHeader?.ParentNode
            .SelectSingleNode(".//span");

        return descriptionNode?.InnerText.Trim();
    }

    /// <summary>
    ///     Выполняет запрос к поисковой системе Google.
    /// </summary>
    /// <param name="query">Термин для поиска.</param>
    /// <returns>Возвращает HTML-код страницы с результатами поиска.</returns>
    private async Task<string> FetchSearchResultsAsync(string query)
    {
        var requestUri = $"https://www.google.com/search?q=что+такое+{query}&ie={Utf8Encoding}";
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgentString);
        httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd(AcceptLanguageHeader);
        return await httpClient.GetStringAsync(requestUri).ConfigureAwait(false);
    }

    /// <summary>
    ///     Форматирует запрос для поисковой системы.
    /// </summary>
    /// <param name="query">Термин для поиска.</param>
    /// <returns>Возвращает отформатированный запрос.</returns>
    private static string FormatQuery(string query)
    {
        query = query
            .Trim()
            .Replace(" ", "+")
            .ToLowerInvariant();
        return query;
    }

    /// <summary>
    ///     Пытается извлечь определение из результатов поиска Google, используя различные методы.
    /// </summary>
    /// <param name="response">HTML-код ответа страницы.</param>
    /// <returns>Возвращает текст определения или <c>null</c>, если определение не найдено.</returns>
    private static string? TryExtractDefinitionFromGoogle(string response)
    {
        var document = new HtmlDocument();
        document.LoadHtml(response);
        return ExtractDefinitionFromDescription(document) ?? ExtractDefinitionFromDictionary(document) ??
            ExtractDefinitionFromHighlightedText(document) ??
            ExtractDefinitionFromVisualDigest(document) ?? null;
    }
}