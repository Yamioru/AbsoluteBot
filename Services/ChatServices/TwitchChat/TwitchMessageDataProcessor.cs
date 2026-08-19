using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services.UtilityServices;
using Serilog;
using TwitchLib.Client.Models;

namespace AbsoluteBot.Services.ChatServices.TwitchChat;

/// <summary>
///     Класс для обработки сообщений с Twitch, включая парсинг сообщений и создание контекста.
/// </summary>
public partial class TwitchMessageDataProcessor(ConfigService configService) : IAsyncInitializable
{
    private string? _commonBotName;
    private string? _twitchBotName;

    public async Task InitializeAsync()
    {
        _commonBotName = await configService.GetConfigValueAsync<string>("BotName").ConfigureAwait(false);
        _twitchBotName = await configService.GetConfigValueAsync<string>("TwitchBotName").ConfigureAwait(false);
        if (string.IsNullOrEmpty(_twitchBotName) || string.IsNullOrEmpty(_commonBotName))
            Log.Warning("Не удалось загрузить имя бота в твич или общее имя бота.");
    }

    /// <summary>
    ///     Парсит сообщение с Twitch и возвращает текст и контекст сообщения.
    /// </summary>
    /// <param name="chatMessage">Сообщение чата Twitch.</param>
    /// <param name="service">Сервис чата Twitch.</param>
    /// <param name="messageText">Текст сообщения.</param>
    /// <param name="context">Контекст чата.</param>
    /// <returns>True, если сообщение валидно, иначе False.</returns>
    public bool TryParseValidMessage(ChatMessage chatMessage, TwitchChatService service, [NotNullWhen(true)] out string? messageText,
        [NotNullWhen(true)] out TwitchChatContext? context)
    {
        context = null;
        messageText = null;
        if (IsOwnBotMessage(chatMessage.Username) || IsOwnBotMessage(chatMessage.DisplayName)) return false;

        messageText = GetMessageText(chatMessage);
        if (string.IsNullOrEmpty(messageText)) return false;

        // Извлечение информации о сообщении-ответе (если есть)
        var replyInfo = GetReplyInfoFromMessage(chatMessage);

        // Создание контекста чата для обработки сообщения
        context = CreateChatContext(chatMessage, service, replyInfo);

        return true;
    }

    /// <summary>
    ///     Создает контекст чата для сообщения с Twitch.
    /// </summary>
    /// <param name="chatMessage">Сообщение чата Twitch.</param>
    /// <param name="service">Сервис чата Twitch.</param>
    /// <param name="replyInfo">Информация о родительском сообщении (если есть).</param>
    /// <returns>Объект контекста чата <see cref="TwitchChatContext" />.</returns>
    private static TwitchChatContext CreateChatContext(ChatMessage chatMessage, TwitchChatService service, ReplyInfo? replyInfo)
    {
        return new TwitchChatContext(
            chatMessage.Username,
            TwitchChatService.MaxMessageLength,
            service,
            chatMessage.Channel,
            chatMessage.Id,
            null,
            replyInfo,
            chatMessage.DisplayName
        );
    }

    [GeneratedRegex(@"^@\S+\s+(!.+)$")]
    private static partial Regex ExtraMentionRegex();

    /// <summary>
    ///     Получает очищенный текст сообщения.
    /// </summary>
    /// <param name="chatMessage">Сообщение чата Twitch.</param>
    /// <returns>Текст сообщения.</returns>
    private string? GetMessageText(ChatMessage chatMessage)
    {
        var messageText = chatMessage.Message;
        messageText = ReplaceBotName(messageText);
        if (string.IsNullOrEmpty(messageText)) return null;

        //Удаление лишних упоминаний и пустых юникод символов
        var match = ExtraMentionRegex().Match(messageText);
        if (match.Success) messageText = match.Groups[1].Value.Trim();
        messageText = InvisibleUnicodeRegex().Replace(messageText, string.Empty);

        return messageText;
    }

    /// <summary>
    ///     Извлекает информацию о родительском сообщении, на которое был дан ответ.
    /// </summary>
    /// <param name="chatMessage">Сообщение чата Twitch.</param>
    /// <returns>
    ///     Объект <see cref="ReplyInfo" />, содержащий информацию о родительском сообщении или null, если ответ
    ///     отсутствует.
    /// </returns>
    private ReplyInfo? GetReplyInfoFromMessage(ChatMessage chatMessage)
    {
        if (chatMessage.ChatReply == null) return null;

        var parentUserLogin = ReplaceBotName(chatMessage.ChatReply.ParentUserLogin);
        var parentMessageBody = ReplaceBotName(chatMessage.ChatReply.ParentMsgBody);

        if (string.IsNullOrEmpty(parentUserLogin) || string.IsNullOrWhiteSpace(parentMessageBody)) return null;

        return new ReplyInfo(parentUserLogin, parentMessageBody);
    }

    [GeneratedRegex(@"\p{C}+")]
    private static partial Regex InvisibleUnicodeRegex();

    /// <summary>
    ///     Проверяет, принадлежит ли никнейм самому боту.
    /// </summary>
    internal bool IsOwnBotMessage(string? username)
    {
        if (string.IsNullOrWhiteSpace(username)) return false;
        return (!string.IsNullOrEmpty(_twitchBotName) &&
                username.Equals(_twitchBotName, StringComparison.InvariantCultureIgnoreCase)) ||
               (!string.IsNullOrEmpty(_commonBotName) &&
                username.Equals(_commonBotName, StringComparison.InvariantCultureIgnoreCase));
    }

    /// <summary>
    ///     Заменяет конкретный никнейм бота на общий.
    /// </summary>
    /// <param name="text">Текст для замены.</param>
    /// <returns>Заменённый текст.</returns>
    private string? ReplaceBotName(string? text)
    {
        if (string.IsNullOrWhiteSpace(_twitchBotName) || string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(_commonBotName))
            return text;
        return text.Replace(_twitchBotName, _commonBotName, StringComparison.InvariantCultureIgnoreCase);
    }
}