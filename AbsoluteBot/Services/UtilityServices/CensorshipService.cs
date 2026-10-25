using System.Collections.Concurrent;
using AbsoluteBot.Helpers;
using Serilog;

namespace AbsoluteBot.Services.UtilityServices;
#pragma warning disable IDE0028
/// <summary>
///     Сервис для цензуры текста. Заменяет запрещенные слова и обрезает текст до указанной длины.
/// </summary>
public class CensorshipService(ConfigService configService, CensorWordsService censorWordsService)
    : ICensorshipService, IAsyncInitializable
{
    private ConcurrentBag<string> _censorWords = new();
    private string _replacementWord = string.Empty;

    public async Task InitializeAsync()
    {
        _replacementWord = await configService.GetConfigValueAsync<string>("ReplacementWordForCensor").ConfigureAwait(false) ?? string.Empty;
        _censorWords = censorWordsService.GetAllCensorWords();
        if (string.IsNullOrEmpty(_replacementWord) || _censorWords.IsEmpty)
            Log.Warning("Не удалось загрузить данные для сервиса CensorshipService.");
    }

    /// <summary>
    ///     Применяет цензуру к тексту, заменяя запрещенные слова и обрезая текст до указанной длины.
    /// </summary>
    /// <param name="text">Исходный текст.</param>
    /// <param name="length">Максимальная длина текста после обработки.</param>
    /// <param name="needToClean">Указывает, нужно ли предварительно очистить текст.</param>
    /// <returns>Цензурированный текст.</returns>
    public string ApplyCensorship(string text, int length, bool needToClean)
    {
        try
        {
            var output = text;
            if (needToClean)
                output = TextProcessingUtils.CleanText(output);

            // Логика замены запрещенных слов и обрезка текста
            output = ApplyWordCensorship(output);
            output = TextProcessingUtils.CutSentence(output, length);
            return output;
        }
        catch (Exception)
        {
            return text;
        }
    }

    /// <summary>
    ///     Заменяет запрещенные слова на замещающее слово.
    /// </summary>
    /// <param name="text">Текст, который нужно обработать.</param>
    /// <returns>Текст с замененными словами.</returns>
    private string ApplyWordCensorship(string text)
    {
        foreach (var word in _censorWords)
        {
            int index;
            // Замена всех вхождений запрещенного слова
            while ((index = text.IndexOf(word, StringComparison.InvariantCultureIgnoreCase)) != -1)
            {
                text = text.Remove(index, word.Length);
                text = text.Insert(index, _replacementWord);
            }
        }

        return text;
    }
}