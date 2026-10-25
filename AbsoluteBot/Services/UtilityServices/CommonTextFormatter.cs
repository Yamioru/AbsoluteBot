namespace AbsoluteBot.Services.UtilityServices;

/// <summary>
///     Форматирует текст для платформ с общими стилями (например, Telegram.
/// </summary>
public class CommonTextFormatter : ITextFormatter
{
    /// <summary>
    ///     Возвращает текст в формате жирного шрифта.
    /// </summary>
    /// <param name="text">Текст для форматирования.</param>
    /// <returns>Отформатированный текст в жирном шрифте.</returns>
    public string FormatBold(string text)
    {
        return $"*{text}*";
    }

    /// <summary>
    ///     Возвращает текст в формате курсива.
    /// </summary>
    /// <param name="text">Текст для форматирования.</param>
    /// <returns>Отформатированный текст в курсиве.</returns>
    public string FormatItalic(string text)
    {
        return $"_{text}_";
    }
}