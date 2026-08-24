using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services.UtilityServices;
using Serilog;

namespace AbsoluteBot.Services.ChatServices.VkPlayLive;
#pragma warning disable IDE0301
#pragma warning disable IDE0028
/// <summary>
///     Класс для обработки сообщений с VkPlayLive, включая парсинг сообщений и создание контекста.
/// </summary>
public class VkPlayMessageDataProcessor(ConfigService configService) : IAsyncInitializable
{
    private const string ContentTypeText = "text";
    private const string ContentTypeMention = "mention";
    private const string MessageType = "message";
    private string? _commonBotName;
    private string? _vkPlayBotName;

    public async Task InitializeAsync()
    {
        _commonBotName = await configService.GetConfigValueAsync<string>("BotName").ConfigureAwait(false);
        _vkPlayBotName = await configService.GetConfigValueAsync<string>("VkPlayBotName").ConfigureAwait(false);
        if (string.IsNullOrEmpty(_vkPlayBotName) || string.IsNullOrEmpty(_commonBotName))
            Log.Warning("Не удалось загрузить имя бота в вк плей или общее имя бота.");
    }

    /// <summary>
    ///     Парсит и проверяет сообщение из VkPlayLive, возвращает текст и контекст сообщения через out.
    /// </summary>
    /// <param name="rawMessage">Сырое сообщение в формате JSON.</param>
    /// <param name="chatService">Сервис чата VkPlay.</param>
    /// <param name="text">Очищенный текст сообщения (через out).</param>
    /// <param name="context">Контекст чата (через out).</param>
    /// <returns>True, если сообщение валидно и данные извлечены, иначе False.</returns>
    public bool TryParseValidMessage(string rawMessage, VkPlayChatService chatService, [NotNullWhen(true)] out string? text,
        [NotNullWhen(true)] out VkPlayChatContext? context)
    {
        text = null;
        context = null;

        if (!TryParseMessage(rawMessage, out var messageData, out var username) || _vkPlayBotName == null ||
            string.Equals(_vkPlayBotName, username, StringComparison.InvariantCultureIgnoreCase)) return false;

        // Извлечение текста сообщения
        text = GetJoinedTextFromMessageData(messageData);
        if (string.IsNullOrEmpty(text)) return false;

        // Извлечение Id сообщения
        var messageId = messageData.MessageId;

        // Извлечение упомянутых никнеймов из сообщения
        var mentionNicknames = ExtractMentionNicknames(messageData);

        // Извлечение ссылок из сообщения
        var urls = ExtractUrlsFromMessageData(messageData);

        // Извлечение информации о родительском сообщении (если есть)
        var replyInfo = GetReplyInfoFromMessageData(messageData);

        // Создание контекста чата
        context = new VkPlayChatContext(
            username,
            VkPlayChatService.MaxMessageLength,
            chatService,
            null,
            replyInfo,
            mentionNicknames,
            messageId,
            urls
        );

        return true;
    }

    /// <summary>
    ///     Извлекает содержимое сообщения в зависимости от его типа (текст или упоминание).
    /// </summary>
    /// <param name="content">Объект содержимого сообщения.</param>
    /// <returns>Текст содержимого или упоминания, если оно валидно, иначе null.</returns>
    private static string? ExtractContent(VkPlayMessageContent content)
    {
        return content.ContentType switch
        {
            ContentTypeText => TryParseContentFromJson(content.Content),
            ContentTypeMention => content.Nickname,
            _ => null
        };
    }

    /// <summary>
    ///     Извлекает никнеймы пользователей, упомянутых в сообщении.
    /// </summary>
    /// <param name="messageData">Данные сообщения VkPlay.</param>
    /// <returns>Список упомянутых никнеймов.</returns>
    private static List<string> ExtractMentionNicknames(VkPlayMessage messageData)
    {
        return messageData.Contents?
            .Where(content => !string.IsNullOrWhiteSpace(content.Nickname))
            .Select(content => content.Nickname!)
            .ToList() ?? new List<string>();
    }

    /// <summary>
    ///     Извлекает текстовые содержимые из родительского сообщения.
    /// </summary>
    /// <param name="messageData">Данные сообщения VkPlay, содержащие информацию о родительском сообщении.</param>
    /// <returns>Перечисление строк с текстами родительского сообщения или пустое перечисление, если текст не найден.</returns>
    private static IEnumerable<string> ExtractParentMessageTexts(VkPlayMessage messageData)
    {
        return messageData.ParentMessage?.Contents?
                   .Where(IsValidTextOrMentionContent)
                   .Select(c => TryParseContentFromJson(c.Content) ?? string.Empty)
               ?? Enumerable.Empty<string>();
    }

    /// <summary>
    ///     Извлекает текстовые содержимые из сообщения VkPlayMessage, фильтруя их по типу и содержимому.
    /// </summary>
    /// <param name="messageData">Данные сообщения VkPlay, из которых нужно извлечь текстовые содержимые.</param>
    /// <returns>Перечисление строк, содержащих тексты, или пустое перечисление, если текст не найден.</returns>
    private static IEnumerable<string> ExtractTextContents(VkPlayMessage messageData)
    {
        return messageData.Contents?
            .Where(IsValidTextOrMentionContent)
            .Select(c => ExtractContent(c) ?? string.Empty) ?? Enumerable.Empty<string>();
    }

    /// <summary>
    ///     Извлекает ссылки из содержимого сообщения и его родительского сообщения, если оно присутствует.
    /// </summary>
    /// <param name="messageData">Данные сообщения VkPlay, содержащие ссылки.</param>
    /// <returns>Список строк, представляющих ссылки в сообщении, сначала из основного сообщения, затем из родительского.</returns>
    private static List<string> ExtractUrlsFromMessageData(VkPlayMessage messageData)
    {
        // Извлечение ссылок из основного сообщения
        var urls = messageData.Contents?
            .Where(content => !string.IsNullOrWhiteSpace(content.Url))
            .Select(content => content.Url!)
            .ToList() ?? new List<string>();
        // Извлечение ссылок из родительского сообщения (если есть)
        var parentUrls = messageData.ParentMessage?.Contents?
            .Where(content => !string.IsNullOrWhiteSpace(content.Url))
            .Select(content => content.Url!)
            .ToList() ?? new List<string>();
        // Объединение ссылок: сначала из основного сообщения, затем из родительского
        urls.AddRange(parentUrls);
        return urls;
    }

    /// <summary>
    ///     Извлекает и объединяет текстовые содержимые из сообщения VkPlayMessage.
    /// </summary>
    /// <param name="messageData">Данные сообщения VkPlay, из которых нужно извлечь текст.</param>
    /// <returns>Объединённый текст всех частей сообщения.</returns>
    private string? GetJoinedTextFromMessageData(VkPlayMessage messageData)
    {
        return ReplaceBotName(string.Join(" ", ExtractTextContents(messageData)).Trim());
    }

    /// <summary>
    ///     Извлекает информацию о родительском сообщении из данных VkPlayLive.
    /// </summary>
    /// <param name="messageData">Данные сообщения VkPlayLive.</param>
    /// <returns>Информация о родительском сообщении, включающая текст и имя пользователя.</returns>
    private ReplyInfo? GetReplyInfoFromMessageData(VkPlayMessage messageData)
    {
        var parentText = ReplaceBotName(JoinParentMessageText(messageData));
        var parentUsername = ReplaceBotName(messageData.ParentMessage?.ParentAuthor?.UserName);

        if (string.IsNullOrEmpty(parentUsername) || string.IsNullOrWhiteSpace(parentText)) return null;
        return new ReplyInfo(parentUsername, parentText);
    }

    /// <summary>
    ///     Проверяет, является ли контент текстовым или упоминанием и не пустым.
    /// </summary>
    /// <param name="content">Объект содержимого сообщения.</param>
    /// <returns>True, если контент является текстовым или упоминанием и не пустым, иначе False.</returns>
    private static bool IsValidTextOrMentionContent(VkPlayMessageContent content)
    {
        return (content.ContentType == ContentTypeText && !string.IsNullOrWhiteSpace(content.Content)) ||
               (content.ContentType == ContentTypeMention && !string.IsNullOrWhiteSpace(content.Nickname));
    }

    /// <summary>
    ///     Извлекает и объединяет текст родительского сообщения.
    /// </summary>
    /// <param name="messageData">Данные сообщения VkPlay, содержащие информацию о родительском сообщении.</param>
    /// <returns>Объединённый текст родительского сообщения или пустая строка, если текст отсутствует.</returns>
    private static string JoinParentMessageText(VkPlayMessage messageData)
    {
        return string.Join(" ", ExtractParentMessageTexts(messageData));
    }

    /// <summary>
    ///     Парсит сырое JSON-сообщение из VkPlay и извлекает объект сообщения, если оно является действительным сообщением.
    /// </summary>
    /// <param name="rawMessage">Сырое сообщение в формате JSON.</param>
    /// <returns>Объект VkPlayMessage, если сообщение успешно распознано. Иначе возвращает null.</returns>
    private static VkPlayMessage? ParseVkPlayMessage(string rawMessage)
    {
        try
        {
            var vkPlayMessageEnvelope = JsonSerializer.Deserialize<VkPlayMessageEnvelope>(rawMessage);
            return vkPlayMessageEnvelope?.Push?.Publication?.MessageContainer?.MessageType == MessageType
                ? vkPlayMessageEnvelope.Push.Publication.MessageContainer.Message
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при парсинге сообщения VkPlayLive.");
            return null;
        }
    }

    /// <summary>
    ///     Заменяет конкретный никнейм бота на общий.
    /// </summary>
    /// <param name="text">Текст для замены.</param>
    /// <returns>Заменённый текст.</returns>
    private string? ReplaceBotName(string? text)
    {
        if (string.IsNullOrWhiteSpace(_vkPlayBotName) || string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(_commonBotName))
            return text;
        return text.Replace(_vkPlayBotName, _commonBotName, StringComparison.InvariantCultureIgnoreCase);
    }

    /// <summary>
    ///     Пытается разобрать JSON-контент сообщения и извлечь текст.
    /// </summary>
    /// <param name="content">Контент сообщения в формате строки (JSON).</param>
    /// <returns>Извлеченный текст или null, если парсинг не удался.</returns>
    private static string? TryParseContentFromJson(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;

        try
        {
            return JsonDocument.Parse(content).RootElement[0].GetString();
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Ошибка при парсинге JSON-содержимого на VkPlayLive: {content}");
            return null;
        }
    }

    /// <summary>
    ///     Пытается парсить валидное сообщение VkPlayLive и извлекает данные.
    /// </summary>
    /// <param name="rawMessage">Сообщение в формате строки (JSON).</param>
    /// <param name="messageData">Извлеченные данные сообщения VkPlayLive.</param>
    /// <param name="username">Имя пользователя, отправившего сообщение.</param>
    /// <returns>True, если сообщение валидно, иначе False.</returns>
    private static bool TryParseMessage(string rawMessage, [NotNullWhen(true)] out VkPlayMessage? messageData,
        [NotNullWhen(true)] out string? username)
    {
        messageData = ParseVkPlayMessage(rawMessage);
        username = messageData?.Author?.UserName;

        return messageData != null && !string.IsNullOrEmpty(username);
    }
}