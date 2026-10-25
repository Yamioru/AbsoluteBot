using AbsoluteBot.Chat.Context;

namespace AbsoluteBot.Services.ChatServices.Interfaces;

/// <summary>
///     Интерфейс для сервисов, которые поддерживают подготовку сообщений перед отправкой. Например симуляция печатания в
///     чате.
/// </summary>
public interface IMessagePreparationService
{
    /// <summary>
    ///     Подготавливает сообщение перед отправкой. Например симуляция печатания в чате.
    /// </summary>
    /// <param name="context">Контекст чата, к которому относится сообщение.</param>
    Task PrepareMessageAsync(ChatContext context);
}