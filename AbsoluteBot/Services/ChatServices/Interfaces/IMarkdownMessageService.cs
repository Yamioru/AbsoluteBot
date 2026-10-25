using AbsoluteBot.Chat.Context;

namespace AbsoluteBot.Services.ChatServices.Interfaces;

/// <summary>
///     Интерфейс для отправки сообщений в формате Markdown.
/// </summary>
public interface IMarkdownMessageService
{
    /// <summary>
    ///     Асинхронно отправляет сообщение в формате Markdown.
    /// </summary>
    /// <param name="message">Текст сообщения в формате Markdown.</param>
    /// <param name="context">Контекст чата, в который отправляется сообщение.</param>
    public Task SendMarkdownMessageAsync(string message, ChatContext context);
}