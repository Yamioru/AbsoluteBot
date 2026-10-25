namespace AbsoluteBot.Services.UtilityServices;

/// <summary>
///     Форматирует текст для платформы Discord с использованием ее синтаксиса.
/// </summary>
public class DiscordTextFormatter : ITextFormatter
{
    /// <summary>
    ///     Возвращает текст в формате жирного шрифта для Discord.
    /// </summary>
    /// <param name="text">Текст для форматирования.</param>
    /// <returns>Отформатированный текст в жирном шрифте для Discord.</returns>
    public string FormatBold(string text)
    {
        return $"**{text}**";
    }

    /// <summary>
    ///     Возвращает текст в формате курсива для Discord.
    /// </summary>
    /// <param name="text">Текст для форматирования.</param>
    /// <returns>Отформатированный текст в курсиве для Discord.</returns>
    public string FormatItalic(string text)
    {
        return $"_{text}_";
    }
}