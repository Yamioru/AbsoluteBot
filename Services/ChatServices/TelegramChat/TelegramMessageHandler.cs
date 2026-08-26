using AbsoluteBot.Chat.Context;
using AbsoluteBot.Services.ChatServices.Interfaces;
using AbsoluteBot.Services.UserManagementServices;
using AbsoluteBot.Services.UtilityServices;
using Serilog;

namespace AbsoluteBot.Services.ChatServices.TelegramChat;

/// <summary>
///     Класс, отвечающий за обработку сообщений Telegram, включая обработку упоминаний бота,
///     отправку сообщений и работу с реакциями.
/// </summary>
public class TelegramMessageHandler(MessageProcessingService messageProcessingService, ConfigService configService, RoleService roleService)
    : BaseMessageHandler(configService, roleService)
{
    private const double DefaultRandomMentionProbability = 0.01;
    private const int WinterStartMonth = 12;
    private const int WinterEndMonth = 2;
    private const double RandomReactionProbability = 0.01;
    private readonly ConfigService _telegramConfigService = configService;
    private string? _stickerId;
    private string? _winterStickerId;

    /// <summary>
    ///     Обрабатывает сообщение, упоминания и отправку сообщений и прочее.
    /// </summary>
    /// <param name="message">Текст сообщения для обработки.</param>
    /// <param name="context">Контекст чата, содержащий информацию о текущем чате.</param>
    /// <param name="edited">Сообщение является изменённой версией старого сообщения.</param>
    /// <returns>Обработанное сообщение.</returns>
    public async Task<string> HandleMessageAsync(string message, ChatContext context, bool edited)
    {
        var handledMessage = await HandleMention(message, context, RandomReactionProbability);
        if (handledMessage == null) return message;
        await RandomHandleMessage(context).ConfigureAwait(false);

        if (!edited)
            SaveLastMessage(context.Username, handledMessage);

        // Обрабатывается сообщение через MessageProcessingService (исправление раскладки, перевод)
        var processedMessage = await messageProcessingService.ProcessMessageAsync(context.Username, handledMessage, context.Platform, context.UserId).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(processedMessage))
            await context.ChatService.SendMessageAsync($"{context.Username}: {processedMessage}", context).ConfigureAwait(false);
        else
            processedMessage = handledMessage;

        return processedMessage;
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync().ConfigureAwait(false);
        _stickerId = await _telegramConfigService.GetConfigValueAsync<string>("TelegramStickerId").ConfigureAwait(false);
        _winterStickerId = await _telegramConfigService.GetConfigValueAsync<string>("TelegramStickerIdWinter").ConfigureAwait(false);
        if (string.IsNullOrEmpty(_stickerId) || string.IsNullOrEmpty(_winterStickerId)) Log.Warning("Не удалось загрузить имена стикеров в телеграм.");
    }
    
    /// <summary>
    ///     Проверяет, упоминается ли бот в тексте сообщения или в ответе на сообщение.
    /// </summary>
    /// <param name="text">Текст сообщения.</param>
    /// <param name="context">Контекст чата, содержащий данные о текущем сеансе чата.</param>
    /// <returns>True, если бот упоминается в сообщении, иначе False.</returns>
    protected override bool IsBotMentioned(string text, ChatContext context)
    {
        return text.Contains("@" + CommonBotName, StringComparison.InvariantCultureIgnoreCase) ||
               (context.Reply != null && context.Reply.Username.Equals(CommonBotName, StringComparison.InvariantCultureIgnoreCase));
    }

    /// <summary>
    ///     Добавляет случайную реакцию на сообщение в чате Telegram.
    /// </summary>
    /// <param name="context">Контекст чата, содержащий данные о текущем сеансе чата.</param>
    /// <returns>Задача, представляющая выполнение операции отправки случайной реакции.</returns>
    private async Task RandomHandleMessage(ChatContext context)
    {
        if (Random.NextDouble() < RandomReactionProbability)
        {
            var currentSticker = DateTime.Now.Month is >= WinterStartMonth or <= WinterEndMonth ? _winterStickerId : _stickerId;
            // Отправка стикера в чат, если он настроен
            if (currentSticker != null && context.ChatService is IStickerSendingService stickerSendingService)
                await stickerSendingService.SendStickerAsync(currentSticker, context).ConfigureAwait(false);
        }
    }
}