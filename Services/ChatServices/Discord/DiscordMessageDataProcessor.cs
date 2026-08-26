using System.Text.RegularExpressions;
using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services.UtilityServices;
using Discord;
using Discord.WebSocket;
using Serilog;

namespace AbsoluteBot.Services.ChatServices.Discord;

/// <summary>
///     Обрабатывает и парсит данные сообщений из Discord.
/// </summary>
public partial class DiscordMessageDataProcessor(DiscordGuildChannelService guildChannelService, ConfigService configService) : IAsyncInitializable
{
    private string? _commonBotName;
    private string? _discordBotName;

    public async Task InitializeAsync()
    {
        _commonBotName = await configService.GetConfigValueAsync<string>("BotName").ConfigureAwait(false);
        _discordBotName = await configService.GetConfigValueAsync<string>("DiscordBotName").ConfigureAwait(false);
        if (string.IsNullOrEmpty(_discordBotName) || string.IsNullOrEmpty(_commonBotName))
            Log.Warning("Не удалось загрузить имя бота в дискорд или общее имя бота.");
    }

    /// <summary>
    ///     Извлекает информацию о сообщении-ответе, на которое был дан ответ (если есть).
    /// </summary>
    /// <param name="message">Сообщение из Discord.</param>
    /// <returns>Информация о родительском сообщении, если оно существует.</returns>
    public ReplyInfo? GetReplyInfoFromMessage(SocketMessage message)
    {
        var replyMessage = (message as IUserMessage)?.ReferencedMessage;

        if (replyMessage == null)
            return null;

        var replyUsername = ReplaceBotName(replyMessage.Author.Username);
        var replyText = ReplaceBotName(replyMessage.Content);
        if (replyUsername == null || replyText == null) return null;
        return new ReplyInfo(replyUsername, replyText);
    }

    /// <summary>
    ///     Парсит сообщение и возвращает текст и контекст для обработки.
    /// </summary>
    /// <param name="message">Сообщение из Discord.</param>
    /// <param name="chatService">Сервис чата Discord.</param>
    /// <returns>Кортеж (text, context), где text - очищенный текст сообщения, а context - контекст чата.</returns>
    public async Task<(string text, DiscordChatContext context)?> TryParseValidMessage(SocketMessage message, DiscordChatService chatService)
    {
        // Проверка валидности сообщения
        if (!IsMessageValid(message)) return null;

        // Извлечение имени пользователя
        var username = message.Author?.Username;
        if (string.IsNullOrEmpty(username)) return null;

        // Очищение текста сообщения от эмодзи и тегов
        var cleanedText = GetCleanedTextFromMessage(message);
        if (string.IsNullOrEmpty(cleanedText)) return null;

        // Извлечение информации о тегах
        var tagList = ExtractTagsFromMessage(message);

        // Извлечение информации о канале и типе гильдии
        var chatType = await guildChannelService.DetermineChatType(message.Channel.Id).ConfigureAwait(false);
        var guildType = await guildChannelService.DetermineGuildType(((SocketGuildChannel) message.Channel).Guild.Id).ConfigureAwait(false);

        // Извлечение информации о сообщении-ответе (если есть)
        var replyInfo = GetReplyInfoFromMessage(message);

        // Создание контекста чата
        var context = new DiscordChatContext(
            username,
            DiscordChatService.MaxMessageLength,
            chatService,
            message.Channel.Id,
            (IUserMessage) message,
            guildType,
            chatType,
            null,
            replyInfo,
            tagList
        );
        if (message.Author != null)
            context.UserId = message.Author.Id.ToString();

        return (cleanedText, context);
    }

    /// <summary>
    ///     Регулярное выражение для удаления тегов эмодзи и других ненужных элементов.
    /// </summary>
    [GeneratedRegex("<[0-9a-zA-Z\\:_@!]*>")]
    private static partial Regex EmojisTagRegex();

    /// <summary>
    ///     Извлекает список тегов из сообщения.
    /// </summary>
    /// <param name="message">Сообщение из Discord.</param>
    /// <returns>Список тегов в сообщении.</returns>
    private static List<string> ExtractTagsFromMessage(SocketMessage message)
    {
        return message.Tags.Select(tag => tag.Value.ToString() ?? string.Empty).ToList();
    }

    /// <summary>
    ///     Извлекает и очищает текст сообщения от эмодзи, тегов и ненужных символов.
    /// </summary>
    /// <param name="message">Сообщение из Discord.</param>
    /// <returns>Очищенный текст сообщения.</returns>
    private string? GetCleanedTextFromMessage(SocketMessage message)
    {
        return ReplaceBotName(EmojisTagRegex().Replace(message.Content, "").Trim());
    }

    /// <summary>
    ///     Проверяет, является ли сообщение валидным для обработки.
    /// </summary>
    /// <param name="message">Сообщение из Discord.</param>
    /// <returns>True, если сообщение валидно, иначе False.</returns>
    private static bool IsMessageValid(SocketMessage message)
    {
        return message.Author != null && !message.Author.IsBot && !string.IsNullOrWhiteSpace(message.Content);
    }

    /// <summary>
    ///     Заменяет конкретный никнейм бота на общий.
    /// </summary>
    /// <param name="text">Текст для замены.</param>
    /// <returns>Заменённый текст.</returns>
    private string? ReplaceBotName(string? text)
    {
        if (string.IsNullOrWhiteSpace(_discordBotName) || string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(_commonBotName))
            return text;
        return text.Replace(_discordBotName, _commonBotName, StringComparison.InvariantCultureIgnoreCase);
    }
}