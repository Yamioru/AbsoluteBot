using System.Text.RegularExpressions;
using System.Web;
using Serilog;

namespace AbsoluteBot.Helpers;

/// <summary>
/// Содержит утилиты для обработки текста, включая очистку от HTML-тегов, обрезку предложений, удаление эмодзи и
/// прочее.
/// </summary>
public static partial class TextProcessingUtils
{
    /// <summary>
    /// Очищает строку от HTML-тегов и декодирует HTML-сущности.
    /// </summary>
    /// <param name="inputText">Входная строка с HTML-тегами.</param>
    /// <returns>Строка без HTML-тегов и с декодированными сущностями.</returns>
    public static string CleanHtmlTags(string inputText)
    {
        if (string.IsNullOrWhiteSpace(inputText)) return string.Empty;

        try
        {
            var textWithoutTags = HtmlTagRegex().Replace(inputText, "").Trim();
            return HttpUtility.HtmlDecode(textWithoutTags);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при очистке текста от html тегов.");
            return inputText;
        }
    }

    /// <summary>
    /// Очищает текст от HTML-тегов, специальных символов и других нежелательных элементов.
    /// </summary>
    /// <param name="inputText">Входной текст для очистки.</param>
    /// <returns>Очищенный текст.</returns>
    public static string CleanText(string inputText)
    {
        if (string.IsNullOrWhiteSpace(inputText)) return string.Empty;

        try
        {
            // Удаление HTML-тегов
            inputText = HtmlTagRegex().Replace(inputText, "").Trim();

            // Удаление управляющих символов и специальных последовательностей
            inputText = UnwantedCharactersRegex().Replace(inputText, "");

            // Замена HTML-сущностей на пробелы
            inputText = HtmlEntityRegex().Replace(inputText, " ");

            // Удаление лишних пробелов
            inputText = ExtraSpacesRegex().Replace(inputText, " ").Trim();

            return inputText;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при очистке текста в CleanText.");
            return string.Empty;
        }
    }

    /// <summary>
    /// Обрезает текст до указанной длины, при этом старается сохранить целостность предложений.
    /// </summary>
    /// <param name="inputText">Входной текст для обрезки.</param>
    /// <param name="maxLength">Максимальная длина текста после обрезки.</param>
    /// <returns>Обрезанный текст.</returns>
    public static string CutSentence(string inputText, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(inputText) || maxLength <= 0) return string.Empty;

        try
        {
            if (inputText.Length <= maxLength) return inputText;
            var lastPunctuationIndex = new[]
            {
                inputText.LastIndexOf('.', maxLength),
                inputText.LastIndexOf('!', maxLength),
                inputText.LastIndexOf('?', maxLength)
            }.Max();

            if (lastPunctuationIndex != -1) return inputText[..(lastPunctuationIndex + 1)];

            var lastSpaceIndex = inputText.LastIndexOf(' ', maxLength);
            if (lastSpaceIndex != -1)
                return inputText[..lastSpaceIndex] + "...";

            return inputText[..maxLength];
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при обрезке текста.");
            return string.Empty;
        }
    }

    /// <summary>
    /// Удаляет эмодзи и другие специальные символы из текста.
    /// </summary>
    /// <param name="message">Текст для обработки.</param>
    /// <returns>Текст без эмодзи и специальных символов.</returns>
    public static string RemoveEmojis(string message)
    {
        return EmojisRegex().Replace(message, string.Empty);
    }

    /// <summary>
    /// Удаляет все символы, кроме буквенных, из текста.
    /// </summary>
    /// <param name="inputText">Входной текст для обработки.</param>
    /// <returns>Текст, содержащий только буквы и пробелы.</returns>
    public static string RemoveNonAlphabeticCharacters(string inputText)
    {
        return NonAlphabeticCharactersRegex().Replace(inputText, string.Empty);
    }

    /// <summary>
    /// Удаляет все символы, кроме букв, цифр и пробелов, из текста.
    /// </summary>
    /// <param name="inputText">Входной текст для обработки.</param>
    /// <returns>Текст, содержащий только буквы, цифры и пробелы.</returns>
    public static string RemoveNonAlphanumericCharacters(string inputText)
    {
        if (string.IsNullOrWhiteSpace(inputText)) return string.Empty;

        try
        {
            return AlphanumericCharactersRegex().Replace(inputText, string.Empty);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при удалении неалфавитно-цифровых символов.");
            return inputText;
        }
    }

    /// <summary>
    /// Обработчик текста перед добавлением в json
    /// </summary>
    /// <param name="input">Входной текст</param>
    /// <returns>Выходной текст</returns>
    public static string SanitizeName(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        // Берём первую непустую строку
        var firstLine = input.Split(new[] {'\r', '\n'}, StringSplitOptions.RemoveEmptyEntries)[0];

        // Убираем возможные маркдауны/кавычки/лишние знаки
        firstLine = Regex.Replace(firstLine, @"[*_`#>\[\](){}~]", string.Empty);
        firstLine = firstLine.Trim(' ', '"', '«', '»', '“', '”', '\'', '.', '!', '?', ':', ';');

        // Схлопываем пробелы
        firstLine = Regex.Replace(firstLine, @"\s{2,}", " ").Trim();

        // Ограничим длину на всякий
        if (firstLine.Length > 40)
            firstLine = firstLine[..40].Trim();

        return firstLine;
    }

    [GeneratedRegex("[^a-zA-Zа-яА-Я0-9 ]")]
    private static partial Regex AlphanumericCharactersRegex();

    [GeneratedRegex(@"[\p{Cs}\p{So}]")]
    private static partial Regex EmojisRegex();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex ExtraSpacesRegex();

    [GeneratedRegex("&[#a-zA-Z0-9]*;")]
    private static partial Regex HtmlEntityRegex();

    [GeneratedRegex("<[^>]+>|&nbsp;")]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex("[^a-zA-Zа-яА-Я ]")]
    private static partial Regex NonAlphabeticCharactersRegex();

    [GeneratedRegex(@"[\\\^\ń]")]
    private static partial Regex UnwantedCharactersRegex();
}