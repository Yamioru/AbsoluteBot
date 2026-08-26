using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services.UtilityServices;
using Serilog;
using Telegram.Bot.Types;

namespace AbsoluteBot.Services.ChatServices.TelegramChat;

/// <summary>
///     Отвечает за обработку данных сообщений Telegram и извлечение необходимой информации.
/// </summary>
public class TelegramMessageDataProcessor(TelegramChannelManager telegramChannelManager, ConfigService configService) : IAsyncInitializable
{
    private string? _commonBotName;
    private string? _telegramBotName;

    public async Task InitializeAsync()
    {
        _commonBotName = await configService.GetConfigValueAsync<string>("BotName").ConfigureAwait(false);
        _telegramBotName = await configService.GetConfigValueAsync<string>("TelegramBotName").ConfigureAwait(false);
        if (string.IsNullOrEmpty(_telegramBotName) || string.IsNullOrEmpty(_commonBotName))
            Log.Warning("Не удалось загрузить имя бота в телеграм или общее имя бота.");
    }

    /// <summary>
    ///     Извлекает информацию о родительском сообщении (если есть), на которое был дан ответ.
    /// </summary>
    /// <param name="replyToMessage">Сообщение, на которое был дан ответ.</param>
    /// <returns>Объект <see cref="ReplyInfo" />, содержащий данные о родительском сообщении.</returns>
    public ReplyInfo? GetReplyInfo(Message? replyToMessage)
    {
        if (replyToMessage == null)
            return null;
        var username = ExtractUsername(replyToMessage);
        var text = replyToMessage.Text;
        text = ReplaceBotName(text);
        if (username == null || text == null) return null;
        return new ReplyInfo(username, text);
    }

    /// <summary>
    ///     Проверяет валидность сообщения, очищает текст и возвращает контекст и текст.
    /// </summary>
    /// <param name="message">Сообщение из Telegram, которое нужно проверить.</param>
    /// <param name="chatService">Сервис чата Telegram.</param>
    /// <returns>Кортеж (text, context), где text - это очищенный текст сообщения, а context - контекст чата.</returns>
    public async Task<(string text, TelegramChatContext context)?> TryParseValidMessage(Message message, TelegramChatService chatService)
    {
        // Извлечение имени пользователя и текста сообщения
        var username = message.From?.Username;
        var cleanedText = ReplaceBotName(message.Text?.Trim());


        // Игнорирование сообщений от ботов или пустого текста
        if (string.IsNullOrWhiteSpace(cleanedText) || message.From == null || message.From.IsBot || string.IsNullOrEmpty(username)) return null;

        // Извлечение информации о канале
        var channelId = message.Chat.Id;

        // Определение типа канала
        var channelType = await telegramChannelManager.DetermineChannelType(channelId).ConfigureAwait(false);
        // Конфигурирование информации о сообщении, на которое был дан ответ (если есть)
        var replyInfo = GetReplyInfo(message.ReplyToMessage);

        // Создание контекста чата
        var context = new TelegramChatContext(
            username,
            TelegramChatService.MaxMessageLength,
            chatService,
            channelId,
            channelType,
            message.MessageId,
            message,
            null,
            replyInfo
        );
        context.UserId = message.From.Id.ToString();

        return (cleanedText, context);
    }

    /// <summary>
    ///     Извлекает имя пользователя из сообщения и удаляет ник бота из имени пользователя.
    /// </summary>
    /// <param name="replyToMessage">Сообщение, на которое был дан ответ.</param>
    /// <returns>Имя пользователя.</returns>
    private string? ExtractUsername(Message replyToMessage)
    {
        var username = replyToMessage.From?.Username;
        return ReplaceBotName(username);
    }

    /// <summary>
    ///     Заменяет конкретный никнейм бота на общий.
    /// </summary>
    /// <param name="text">Текст для замены.</param>
    /// <returns>Заменённый текст.</returns>
    private string? ReplaceBotName(string? text)
    {
        if (string.IsNullOrWhiteSpace(_telegramBotName) || string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(_commonBotName))
            return text;
        return text.Replace(_telegramBotName, _commonBotName, StringComparison.InvariantCultureIgnoreCase);
    }
}