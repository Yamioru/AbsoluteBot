using AbsoluteBot.Services.UtilityServices;

namespace AbsoluteBot.Services.ChatServices;

/// <summary>
///     Сервис для обработки сообщений, который выполняет исправление раскладки и автоматический перевод текста.
/// </summary>
public class MessageProcessingService(LayoutCorrectionService layoutCorrectionService, AutoTranslateService autoTranslateService)
{
    /// <summary>
    ///     Обрабатывает сообщение, исправляя раскладку и переводя текст, если необходимо.
    /// </summary>
    /// <param name="username">Имя пользователя, отправившего сообщение.</param>
    /// <param name="text">Текст сообщения.</param>
    /// <returns>Возвращает исправленный или переведенный текст сообщения или null, если текст не изменился.</returns>
    public Task<string?> ProcessMessageAsync(string username, string text) =>
        ProcessMessageAsync(username, text, null, null);

    public async Task<string?> ProcessMessageAsync(string username, string text, string? platform, string? userId)
    {
        // Исправление раскладки
        var correctedText = layoutCorrectionService.CorrectLayoutIfNeeded(text);

        // Если раскладка была изменена, возвращается сообщение с исправленной раскладкой
        if (correctedText != text) return correctedText;

        // Проверка необходимости перевода сообщения
        var translatedText = await autoTranslateService.TranslateUserMessageAsync(username, text, platform, userId).ConfigureAwait(false);
        if (translatedText == null || translatedText == text) return null;
        return translatedText;
    }
}