using AbsoluteBot.Models;
using AbsoluteBot.Services.ChatServices.Interfaces;
using AbsoluteBot.Services.UtilityServices;

namespace AbsoluteBot.Chat.Context;

/// <summary>
///     Контекст для чата на платформе Twitch, содержащий информацию о пользователе, канале и ролях пользователя.
/// </summary>
/// <param name="username">Имя пользователя, который взаимодействует с ботом.</param>
/// <param name="maxMessageLength">Максимально допустимая длина сообщения на платформе Twitch.</param>
/// <param name="chatService">Сервис чата, используемый для отправки и обработки сообщений.</param>
/// <param name="channel">Канал, в котором происходит общение.</param>
/// <param name="messageId">Идентификатор полученного сообщения.</param>
/// <param name="lastMessages">Список последних сообщений в чате (опционально).</param>
/// <param name="replyInfo">Информация о сообщении, на которое дается ответ (опционально).</param>
public class TwitchChatContext(string username, int maxMessageLength, IChatService chatService,
        string channel, string messageId, List<string>? lastMessages, ReplyInfo? replyInfo, string displayedName)
    : ChatContext("Twitch", username, maxMessageLength, chatService, lastMessages, replyInfo, new CommonTextFormatter())
{
    /// <summary>
    ///     Канал, в котором происходит общение.
    /// </summary>
    public string Channel { get; set; } = channel;
    /// <summary>
    ///     Идентификатор полученного сообщения.
    /// </summary>
    public string MessageId { get; set; } = messageId;
    /// <summary>
    ///     Отображаемое имя пользователя
    /// </summary>
    public string DisplayedName { get; set; } = displayedName;
}