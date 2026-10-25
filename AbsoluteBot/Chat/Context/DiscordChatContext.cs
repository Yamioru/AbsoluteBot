using AbsoluteBot.Models;
using AbsoluteBot.Services.ChatServices.Interfaces;
using AbsoluteBot.Services.UtilityServices;
using Discord;

namespace AbsoluteBot.Chat.Context;

/// <summary>
///     Контекст для чата на платформе Discord, содержащий информацию о сообщении, канале, типе гильдии и канала.
/// </summary>
/// <param name="username">Имя пользователя, который взаимодействует с ботом.</param>
/// <param name="maxMessageLength">Максимально допустимая длина сообщения на платформе Discord.</param>
/// <param name="chatService">Сервис чата, используемый для отправки и обработки сообщений.</param>
/// <param name="channelId">Идентификатор канала, в котором происходит общение.</param>
/// <param name="userMessage">Объект сообщения, отправленного пользователем.</param>
/// <param name="guidType">Тип гильдии, к которой принадлежит канал.</param>
/// <param name="chatType">Тип чата в Discord.</param>
/// <param name="lastMessages">Список последних сообщений в чате (опционально).</param>
/// <param name="replyInfo">Информация о сообщении, на которое дается ответ (опционально).</param>
/// <param name="tagList">Список тегов в сообщении пользователя.</param>
public class DiscordChatContext(string username, int maxMessageLength, IChatService chatService, ulong channelId,
        IUserMessage userMessage, ChannelType guidType, ChannelType chatType, List<string>? lastMessages,
        ReplyInfo? replyInfo, List<string> tagList)
    : ChatContext("Discord", username, maxMessageLength, chatService, lastMessages, replyInfo, new DiscordTextFormatter())
{
    /// <summary>
    ///     Тип чата в Discord.
    /// </summary>
    public ChannelType ChatType { get; } = chatType;
    /// <summary>
    ///     Тип гильдии в Discord.
    /// </summary>
    public ChannelType GuildType { get; } = guidType;
    /// <summary>
    ///     Объект сообщения, отправленного пользователем.
    /// </summary>
    public IUserMessage? UserMessage { get; } = userMessage;
    /// <summary>
    ///     Список тегов в сообщении пользователя.
    /// </summary>
    public List<string> TagList { get; } = tagList;
    /// <summary>
    ///     Идентификатор канала, в котором происходит общение.
    /// </summary>
    public ulong ChannelId { get; } = channelId;
}