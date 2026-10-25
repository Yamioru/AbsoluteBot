namespace AbsoluteBot.Services.UtilityServices;

/// <summary>
///     Интерфейс для форматирования текста в зависимости от платформы.
/// </summary>
public interface ITextFormatter
{
    /// <summary>
    ///     Форматирует текст как жирный.
    /// </summary>
    /// <param name="text">Текст для форматирования.</param>
    /// <returns>Отформатированный текст в жирном шрифте.</returns>
    string FormatBold(string text);

    /// <summary>
    ///     Форматирует текст как курсив.
    /// </summary>
    /// <param name="text">Текст для форматирования.</param>
    /// <returns>Отформатированный текст в курсиве.</returns>
    string FormatItalic(string text);
}