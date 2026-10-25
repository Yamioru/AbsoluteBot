using AbsoluteBot.Chat.Context;

namespace AbsoluteBot.Services.ChatServices.Interfaces;

/// <summary>
///     Интерфейс для чат-сервисов, которые поддерживают отправку стикеров.
/// </summary>
public interface IStickerSendingService
{
    /// <summary>
    ///     Отправляет стикер.
    /// </summary>
    /// <param name="sticker">Стикер для отправки.</param>
    /// <param name="context">Контекст чата.</param>
    public Task SendStickerAsync(string sticker, ChatContext context);
}