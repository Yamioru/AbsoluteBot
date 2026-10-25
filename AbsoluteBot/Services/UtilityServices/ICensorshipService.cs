namespace AbsoluteBot.Services.UtilityServices;

/// <summary>
///     Интерфейс для сервиса цензуры текста.
///     Осуществляет замену запрещенных слов и обрезку текста до указанной длины.
/// </summary>
public interface ICensorshipService
{
    /// <summary>
    ///     Применяет цензуру к тексту, заменяя запрещенные слова и обрезая текст до указанной длины.
    /// </summary>
    /// <param name="text">Исходный текст для обработки.</param>
    /// <param name="length">Максимальная длина текста после обработки.</param>
    /// <param name="needToClean">Указывает, нужно ли предварительно очистить текст.</param>
    /// <returns>Цензурированный и обработанный текст.</returns>
    string ApplyCensorship(string text, int length, bool needToClean);
}