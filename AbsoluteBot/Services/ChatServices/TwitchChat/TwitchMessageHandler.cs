using AbsoluteBot.Chat.Context;
using AbsoluteBot.Services.ChatServices.VkPlayLive;
using AbsoluteBot.Services.UserManagementServices;
using AbsoluteBot.Services.UtilityServices;
using Serilog;

namespace AbsoluteBot.Services.ChatServices.TwitchChat;

/// <summary>
/// Обрабатывает сообщения Twitch, выполняя различные действия по обработке.
/// </summary>
public class TwitchMessageHandler(MessageProcessingService messageProcessingService, ICensorshipService censorshipService,
    ConfigService configService,
    BirthdayService birthdayService, VkPlayChatService vkPlayChatService, RoleService roleService) : BirthdayBaseMessageHandler(configService,
    birthdayService, roleService)
{
    private const double RandomMessageProbability = 0.0015;
    private readonly ConfigService _twitchConfigService = configService;
    private string? _defaultEmoteName;

    /// <summary>
    /// Обрабатывает сообщение, применяя цензуру, упоминания и отправку сообщений и прочее.
    /// </summary>
    /// <param name="message">Текст сообщения для обработки.</param>
    /// <param name="context">Контекст чата, содержащий информацию о текущем чате.</param>
    /// <returns>Обработанное сообщение.</returns>
    public async Task<string> HandleMessageAsync(string message, ChatContext context)
    {
        await BirthdayHandle(context, context.Username).ConfigureAwait(false);
        var handledMessage = await HandleMention(message, context);
        if (handledMessage == null) return message;
        SaveLastMessage(context.Username, handledMessage);
        await RandomHandleMessage(context).ConfigureAwait(false);
        // Обрабатывается сообщение через MessageProcessingService (исправление раскладки, перевод)
        var processedMessage = await messageProcessingService.ProcessMessageAsync(context.Username, handledMessage, context.Platform, context.UserId).ConfigureAwait(false);
        if (processedMessage != null)
        {
            // Применение цензуры
            processedMessage = censorshipService.ApplyCensorship(processedMessage, TwitchChatService.MaxMessageLength, true);
            await context.ChatService.SendMessageAsync($"{context.Username}: {processedMessage}", context).ConfigureAwait(false);
        }
        else
        {
            processedMessage = handledMessage;
        }

        if (context is TwitchChatContext twitchChatContext)
            await vkPlayChatService.SendMessageToChannelAsync($"[Twitch] {twitchChatContext.DisplayedName}: {processedMessage}");

        return processedMessage;
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync().ConfigureAwait(false);
        _defaultEmoteName = await _twitchConfigService.GetConfigValueAsync<string>("BaseEmoteTwitchName").ConfigureAwait(false);
        if (string.IsNullOrEmpty(_defaultEmoteName)) Log.Warning("Не удалось загрузить базовый эмоут в твитч.");
    }

    /// <summary>
    /// Проверяет, упоминается ли бот в тексте сообщения или в ответе на сообщение.
    /// </summary>
    /// <param name="text">Текст сообщения.</param>
    /// <param name="context">Контекст чата, содержащий данные о текущем сеансе чата.</param>
    /// <returns>True, если бот упоминается в сообщении, иначе False.</returns>
    protected override bool IsBotMentioned(string text, ChatContext context)
    {
        if (string.IsNullOrEmpty(_defaultEmoteName)) return false;
        return text.Contains("@" + CommonBotName, StringComparison.InvariantCultureIgnoreCase) ||
               (context.Reply != null && context.Reply.Username.Equals(CommonBotName, StringComparison.InvariantCultureIgnoreCase));
    }

    /// <summary>
    /// Случайным образом добавляет сообщение в чат с базовым эмоутом из настроек.
    /// </summary>
    /// <param name="context">Контекст чата, содержащий данные о текущем сеансе чата.</param>
    private async Task RandomHandleMessage(ChatContext context)
    {
        if (Random.NextDouble() < RandomMessageProbability && _defaultEmoteName != null)
            await context.ChatService.SendMessageAsync(_defaultEmoteName, context).ConfigureAwait(false);
    }
}