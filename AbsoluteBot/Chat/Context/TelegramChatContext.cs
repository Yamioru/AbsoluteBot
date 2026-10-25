using AbsoluteBot.Models;
using AbsoluteBot.Services.ChatServices.Interfaces;
using AbsoluteBot.Services.UtilityServices;
using Telegram.Bot.Types;

namespace AbsoluteBot.Chat.Context;

/// <summary>
///     Контекст для чата на платформе Telegram, содержащий информацию о сообщении, канале и типе канала.
/// </summary>
/// <param name="username">Имя пользователя, который взаимодействует с ботом.</param>
/// <param name="maxMessageLength">Максимально допустимая длина сообщения на платформе Telegram.</param>
/// <param name="chatService">Сервис чата, используемый для отправки и обработки сообщений.</param>
/// <param name="channelId">Идентификатор канала, в котором происходит общение.</param>
/// <param name="channelType">Тип канала в Telegram (например, премиум, административный, анонсовый).</param>
/// <param name="messageId">Идентификатор сообщения, на которое реагирует бот.</param>
/// <param name="message">Объект сообщения, отправленного пользователем.</param>
/// <param name="lastMessages">Список последних сообщений в чате (опционально).</param>
/// <param name="replyInfo">Информация о сообщении, на которое дается ответ (опционально).</param>
public class TelegramChatContext(string username, int maxMessageLength, IChatService chatService, long channelId,
        ChannelType channelType, int messageId, Message message, List<string>? lastMessages, ReplyInfo? replyInfo, bool isSpoilerMessage = false)
    : ChatContext("Telegram", username, maxMessageLength, chatService, lastMessages, replyInfo,
        new CommonTextFormatter())
{
    /// <summary>
    ///     Тип канала в Telegram.
    /// </summary>
    public ChannelType ChannelType { get; set; } = channelType;
    /// <summary>
    ///     Идентификатор сообщения, на которое реагирует бот.
    /// </summary>
    public int MessageId { get; set; } = messageId;
    /// <summary>
    ///     Идентификатор канала, в котором происходит общение.
    /// </summary>
    public long ChannelId { get; set; } = channelId;
    /// <summary>
    ///     Сообщение пользователя, с которым бот взаимодействует.
    /// </summary>
    public Message Message { get; set; } = message;

    public bool isSpoilerMessage { get; set; } = isSpoilerMessage;
}