using AbsoluteBot.Chat.Context;

namespace AbsoluteBot.Services.ChatServices.Interfaces;

/// <summary>
///     Интерфейс, определяющий метод для получения изображения в формате Base64
///     из контекста чата.
/// </summary>
public interface IChatImageService
{
    /// <summary>
    ///     Асинхронно извлекает изображение в формате Base64 из контекста чата,
    ///     если оно существует.
    /// </summary>
    /// <param name="message">Полученное сообщение в чате.</param>
    /// <param name="context">Контекст чата, содержащий информацию о сообщении и вложениях.</param>
    /// <returns>Строка в формате Base64, представляющая изображение, или <c>null</c>, если изображение отсутствует.</returns>
    Task<string?> GetImageAsBase64Async(string message, ChatContext context);
}