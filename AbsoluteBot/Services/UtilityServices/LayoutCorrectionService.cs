using System.Text;
using AbsoluteBot.Helpers;
using Serilog;
using WeCantSpell.Hunspell;

namespace AbsoluteBot.Services.UtilityServices;
#pragma warning disable IDE0300
/// <summary>
///     Сервис для исправления раскладки текста. Определяет, нужно ли исправлять раскладку, и выполняет исправление.
/// </summary>
public class LayoutCorrectionService : IAsyncInitializable
{
    private static readonly char[] SplitCharacters = {' ', '\t', '\r', '\n'};
    private readonly Dictionary<char, char> _layoutMapping = InitializeLayoutMapping();
    private WordList? _hunspellEn;
    private WordList? _hunspellRu;

    public async Task InitializeAsync()
    {
        try
        {
            _hunspellEn = await LoadWordListAsync("en_US.dic", "en_US.aff").ConfigureAwait(false);
            _hunspellRu = await LoadWordListAsync("ru_RU.dic", "ru_RU.aff").ConfigureAwait(false);
            if (_hunspellEn == null || _hunspellRu == null) Log.Warning("Hunspell словари не инициализированы.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Словари для исправления раскладки не были инициализированы.");
        }
    }

    /// <summary>
    ///     Проверяет текст и при необходимости исправляет раскладку клавиатуры.
    /// </summary>
    /// <param name="typedText">Введенный текст, который нужно проверить и, если необходимо, исправить раскладку.</param>
    /// <returns>Текст с исправленной раскладкой, если исправление было нужно, или оригинальный текст.</returns>
    public string CorrectLayoutIfNeeded(string typedText)
    {
        try
        {
            if (_hunspellEn == null || _hunspellRu == null) return typedText;
            if (!ContainsEnglishLetters(typedText)) return typedText;

            // Получение текста в двух раскладках
            var (comparisonText, outputText) = ConvertLayout(typedText);
            return SelectCorrectLayout(typedText, comparisonText, outputText, _hunspellRu, _hunspellEn);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при проверки на изменении раскладки.");
            return typedText;
        }
    }

    /// <summary>
    ///     Проверяет, содержит ли текст английские буквы.
    /// </summary>
    /// <param name="text">Текст для проверки на наличие английских букв.</param>
    /// <returns>True, если в тексте есть английские буквы; иначе False.</returns>
    private static bool ContainsEnglishLetters(string text)
    {
        return text.Any(c => char.IsLetter(c) && c is >= 'A' and <= 'Z' or >= 'a' and <= 'z');
    }

    /// <summary>
    ///     Преобразует текст с английской раскладки на русскую.
    /// </summary>
    /// <param name="text">Текст на английской раскладке.</param>
    /// <returns>
    ///     Кортеж из двух строк:
    ///     <c>comparisonText</c> — текст для сравнения, приведенный к нижнему регистру,
    ///     и <c>outputText</c> — текст для отображения пользователю с сохранением регистра.
    /// </returns>
    private (string comparisonText, string outputText) ConvertLayout(string text)
    {
        var comparisonText = ConvertToRussian(text.ToLower());
        var outputText = ConvertToRussianWithCase(text);
        return (comparisonText, outputText);
    }

    /// <summary>
    ///     Преобразует текст на английской раскладке в эквивалентный текст на русской, без сохранения регистра и без символов
    ///     не
    ///     подлежащих сравнению.
    /// </summary>
    /// <param name="text">Текст на английской раскладке.</param>
    /// <returns>Текст, преобразованный для использования в сравнениях.</returns>
    private string ConvertToRussian(string text)
    {
        var builder = new StringBuilder();
        foreach (var character in text)
            if (_layoutMapping.TryGetValue(character, out var russianChar))
                builder.Append(russianChar);
        return builder.ToString();
    }

    /// <summary>
    ///     Преобразует текст с английской раскладки на русскую, оставляя оригинальный регистр и символы неподлежащие изменению
    ///     раскладки.
    /// </summary>
    /// <param name="text">Текст на английской раскладке для преобразования с сохранением регистра.</param>
    /// <returns>Преобразованный текст на русской раскладке с сохранением регистра букв и необработанных символов.</returns>
    private string ConvertToRussianWithCase(string text)
    {
        var builder = new StringBuilder();
        foreach (var character in text)
        {
            var lowerChar = char.ToLower(character);
            if (_layoutMapping.TryGetValue(lowerChar, out var russianChar))
                builder.Append(char.IsUpper(character) ? char.ToUpper(russianChar) : russianChar);
            else
                builder.Append(character);
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Подсчитывает количество корректных слов в тексте с использованием словаря Hunspell.
    /// </summary>
    /// <param name="textToCheck">Текст, который нужно проверить на корректность слов.</param>
    /// <param name="wordList">Словарь для проверки слов (английский или русский).</param>
    /// <returns>Количество корректных слов в тексте.</returns>
    private static int CountCorrectWords(string textToCheck, WordList wordList)
    {
        // Удаление всех символов, кроме буквенных
        textToCheck = TextProcessingUtils.RemoveNonAlphabeticCharacters(textToCheck);
        var words = textToCheck.Split(SplitCharacters, StringSplitOptions.RemoveEmptyEntries);

        // Подсчет слов, которые есть в словаре
        return words.Count(word => wordList.Check(word) && word.Length > 3);
    }

    /// <summary>
    ///     Инициализирует словарь соответствий символов английской раскладки русским.
    /// </summary>
    /// <returns>Словарь, который содержит соответствие английских символов русским.</returns>
    private static Dictionary<char, char> InitializeLayoutMapping()
    {
        return new Dictionary<char, char>
        {
            {'q', 'й'}, {'w', 'ц'}, {'e', 'у'}, {'r', 'к'}, {'t', 'е'}, {'y', 'н'}, {'u', 'г'},
            {'i', 'ш'}, {'o', 'щ'}, {'p', 'з'}, {'[', 'х'}, {']', 'ъ'},
            {'a', 'ф'}, {'s', 'ы'}, {'d', 'в'}, {'f', 'а'}, {'g', 'п'}, {'h', 'р'}, {'j', 'о'},
            {'k', 'л'}, {'l', 'д'}, {';', 'ж'}, {'\'', 'э'},
            {'z', 'я'}, {'x', 'ч'}, {'c', 'с'}, {'v', 'м'}, {'b', 'и'}, {'n', 'т'}, {'m', 'ь'},
            {',', 'б'}, {'.', 'ю'}, {'/', '.'}, {' ', ' '}, {'`', 'ё'}, {'?', ','}, {'&', '?'}, {'{', 'х'}, {'>', 'ю'}, {':', 'ж'}, {'\"', 'э'}
        };
    }

    /// <summary>
    ///     Загружает словарь Hunspell из файлов.
    /// </summary>
    /// <param name="dicPath">Путь к файлу словаря (dic).</param>
    /// <param name="affPath">Путь к файлу аффиксов (aff).</param>
    /// <returns>Словарь <see cref="WordList" />, созданный на основе переданных файлов.</returns>
    private static async Task<WordList> LoadWordListAsync(string dicPath, string affPath)
    {
        await using var dictionaryStream = File.OpenRead(dicPath);
        await using var affixStream = File.OpenRead(affPath);
        return await WordList.CreateFromStreamsAsync(dictionaryStream, affixStream);
    }

    /// <summary>
    ///     Определяет, какой вариант текста оставить — оригинальный или с исправленной раскладкой.
    /// </summary>
    /// <param name="originalText">Оригинальный текст.</param>
    /// <param name="comparisonText">Текст, преобразованный для внутреннего сравнения (в нижнем регистре).</param>
    /// <param name="convertedText">Текст, преобразованный для отображения пользователю (с учетом регистра).</param>
    /// <param name="hunspellRu">Словарь правописания русских слов.</param>
    /// <param name="hunspellEn">Словарь правописания английских слов.</param>
    /// <returns>Либо оригинальный текст, либо исправленный, в зависимости от количества правильных слов.</returns>
    private static string SelectCorrectLayout(string originalText, string comparisonText, string convertedText, WordList hunspellRu, WordList hunspellEn)
    {
        var correctInRussian = CountCorrectWords(comparisonText, hunspellRu);
        var correctInEnglish = CountCorrectWords(originalText, hunspellEn);

        return correctInRussian >= correctInEnglish && correctInRussian > 0 ? convertedText : originalText;
    }
}