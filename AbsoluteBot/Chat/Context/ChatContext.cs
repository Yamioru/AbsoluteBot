using AbsoluteBot.Models;
using AbsoluteBot.Services.ChatServices.Interfaces;
using AbsoluteBot.Services.UtilityServices;

namespace AbsoluteBot.Chat.Context;

/// <summary>
///     Абстрактный класс, представляющий контекст чата, содержащий информацию о текущем сеансе общения.
///     Используется для хранения данных о платформе, пользователе, сервисе чата и дополнительной информации о сообщениях.
/// </summary>
/// <param name="platform">Платформа, на которой происходит общение (например, Discord, Twitch, Telegram и т.д.).</param>
/// <param name="username">Имя пользователя, который взаимодействует с ботом.</param>
/// <param name="maxMessageLength">Максимально допустимая длина сообщения на данной платформе.</param>
/// <param name="chatService">Сервис чата, который используется для отправки и обработки сообщений.</param>
/// <param name="lastMessages">Список последних сообщений в чате (опционально).</param>
/// <param name="replyInfo">Информация о сообщении, на которое дается ответ (опционально).</param>
/// <param name="textFormatter">Сервис форматирования текста.</param>
public abstract class ChatContext(string platform, string username, int maxMessageLength, IChatService chatService,
    List<string>? lastMessages, ReplyInfo? replyInfo, ITextFormatter textFormatter)
{
    /// <summary>
    ///     Сервис чата, используемый для взаимодействия с платформой.
    /// </summary>
    public IChatService ChatService { get; set; } = chatService;
    /// <summary>
    ///     Максимально допустимая длина сообщения на данной платформе.
    /// </summary>
    public int MaxMessageLength { get; set; } = maxMessageLength;
    /// <summary>
    ///     Сервис для форматирования текста.
    /// </summary>
    public ITextFormatter TextFormatter { get; set; } = textFormatter;
    /// <summary>
    ///     Список последних сообщений в чате. Может использоваться для контекста.
    /// </summary>
    public List<string>? LastMessages { get; set; } = lastMessages;
    /// <summary>
    ///     Информация о сообщении, на которое дается ответ. Может быть <c>null</c>, если ответ не предполагается.
    /// </summary>
    public ReplyInfo? Reply { get; set; } = replyInfo;
    /// <summary>
    ///     Платформа, на которой происходит общение (например, Discord, Twitch, Telegram и т.д.).
    /// </summary>
    public string Platform { get; set; } = platform;
    /// <summary>
    ///     Имя пользователя, который взаимодействует с ботом.
    /// </summary>
    public string Username { get; set; } = username;
    /// <summary>
    ///     Стабильный id пользователя на платформе (Twitch UserId, VK Live author.id, Telegram/Discord snowflake).
    /// </summary>
    public string? UserId { get; set; }
}