using AbsoluteBot.Models;
using AbsoluteBot.Services.ChatServices.Interfaces;
using AbsoluteBot.Services.UtilityServices;

namespace AbsoluteBot.Chat.Context;

/// <summary>
///     Контекст для чата на платформе VkPlayLive, содержащий информацию о пользователе и канале.
/// </summary>
/// <param name="username">Имя пользователя, который взаимодействует с ботом.</param>
/// <param name="maxMessageLength">Максимально допустимая длина сообщения на платформе VkPlayLive.</param>
/// <param name="chatService">Сервис чата, используемый для отправки и обработки сообщений.</param>
/// <param name="lastMessages">Список последних сообщений в чате (опционально).</param>
/// <param name="replyInfo">Информация о сообщении, на которое дается ответ (опционально).</param>
/// <param name="mentionNicknames">Упоминаемые в сообщении никнеймы.</param>
public class VkPlayChatContext(string username, int maxMessageLength, IChatService chatService,
        List<string>? lastMessages, ReplyInfo? replyInfo, List<string> mentionNicknames, int messageId, List<string> urls)
    : ChatContext("VkPlayLive", username, maxMessageLength, chatService, lastMessages, replyInfo,
        new CommonTextFormatter())
{
    /// <summary>
    ///     Идентификатор сообщения.
    /// </summary>
    public int MessageId { get; } = messageId;
    /// <summary>
    ///     Никнеймы упоминаемые в сообщении.
    /// </summary>
    public List<string> MentionNicknames { get; } = mentionNicknames;
    /// <summary>
    ///     Список ссылок сообщения.
    /// </summary>
    public List<string> Urls { get; } = urls;
}