using AbsoluteBot.Chat.Context;
using AbsoluteBot.Services.UserManagementServices;
using AbsoluteBot.Services.UtilityServices;

namespace AbsoluteBot.Services.ChatServices.VkPlayLive;

/// <summary>
///     Обрабатывает сообщения VkPlayLive, выполняя различные действия по обработке.
/// </summary>
public class VkPlayMessageHandler(MessageProcessingService messageProcessingService, ICensorshipService censorshipService, BirthdayService birthdayService,
    ConfigService configService, RoleService roleService) : BirthdayBaseMessageHandler(configService, birthdayService, roleService)
{
    /// <summary>
    ///     Обрабатывает сообщение, применяя цензуру, упоминания и отправку сообщений и прочее.
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

        // Обрабатывается сообщение через MessageProcessingService (исправление раскладки, перевод)
        var processedMessage = await messageProcessingService.ProcessMessageAsync(context.Username, handledMessage, context.Platform, context.UserId).ConfigureAwait(false);
        if (processedMessage != null)
        {
            // Применение цензуры
            processedMessage = censorshipService.ApplyCensorship(processedMessage, VkPlayChatService.MaxMessageLength, true);
            await context.ChatService.SendMessageAsync($"{context.Username}: {processedMessage}", context).ConfigureAwait(false);
        }
        else
        {
            processedMessage = handledMessage;
        }

        return processedMessage;
    }

    /// <summary>
    ///     Проверяет, упоминается ли бот в тексте сообщения или в ответе на сообщение.
    /// </summary>
    /// <param name="text">Текст сообщения.</param>
    /// <param name="context">Контекст чата, содержащий данные о текущем сеансе чата.</param>
    /// <returns>True, если бот упоминается в сообщении, иначе False.</returns>
    protected override bool IsBotMentioned(string text, ChatContext context)
    {
        if (context is not VkPlayChatContext vkPlayChatContext || string.IsNullOrEmpty(CommonBotName)) return false;
        return text.Contains(CommonBotName, StringComparison.InvariantCultureIgnoreCase) ||
               (context.Reply != null && context.Reply.Username.Equals(CommonBotName, StringComparison.InvariantCultureIgnoreCase)) ||
               vkPlayChatContext.MentionNicknames.Any(nickname => nickname.Contains(CommonBotName, StringComparison.InvariantCultureIgnoreCase));
    }
}