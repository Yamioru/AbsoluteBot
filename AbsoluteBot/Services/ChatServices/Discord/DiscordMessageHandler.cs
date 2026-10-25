using AbsoluteBot.Chat.Context;
using AbsoluteBot.Services.UserManagementServices;
using AbsoluteBot.Services.UtilityServices;
using Discord;
using Discord.WebSocket;
using Serilog;

namespace AbsoluteBot.Services.ChatServices.Discord;

///// <summary>
/////     Обрабатывает сообщения Discord, выполняя различные действия по обработке.
///// </summary>
public class DiscordMessageHandler(MessageProcessingService messageProcessingService, ConfigService configService, BirthdayService birthdayService, RoleService roleService) :
    BirthdayBaseMessageHandler(configService, birthdayService, roleService)
{
    private const double RandomReactionProbability = 0.015;

    /// <summary>
    ///     Обрабатывает сообщение, упоминания и отправку сообщений и прочее.
    /// </summary>
    /// <param name="message">Текст сообщения для обработки.</param>
    /// <param name="context">Контекст чата, содержащий информацию о текущем чате.</param>
    /// <returns>Обработанное сообщение.</returns>
    public async Task<string> HandleMessageAsync(string message, DiscordChatContext context)
    {
        await BirthdayHandle(context, context.Username).ConfigureAwait(false);
        var handledMessage = await HandleMention(message, context);
        if (handledMessage == null) return message;
        await RandomHandleMessageAsync(context).ConfigureAwait(false);
        SaveLastMessage(context.Username, handledMessage);

        // Обрабатывается сообщение через MessageProcessingService (исправление раскладки, перевод)
        var processedMessage = await messageProcessingService.ProcessMessageAsync(context.Username, handledMessage, context.Platform, context.UserId).ConfigureAwait(false);
        if (processedMessage != null)
            await context.ChatService.SendMessageAsync($"{context.Username}: {processedMessage}", context).ConfigureAwait(false);
        else
            processedMessage = handledMessage;

        return processedMessage;
    }

    /// <summary>
    ///     Проверяет, упоминается ли бот в тексте сообщения или в ответе на сообщение.
    /// </summary>
    /// <param name="text">Текст сообщения.</param>
    /// <param name="context">Контекст чата для текущего сообщения.</param>
    /// <returns>True, если бот упоминается в сообщении, иначе False.</returns>
    protected override bool IsBotMentioned(string text, ChatContext context)
    {
        if (context is not DiscordChatContext discordChatContext || string.IsNullOrEmpty(CommonBotName)) return false;
        return text.Contains("@" + CommonBotName, StringComparison.InvariantCultureIgnoreCase)
               || (context.Reply != null && context.Reply.Username.Equals(CommonBotName, StringComparison.InvariantCultureIgnoreCase))
               || discordChatContext.TagList.Any(tag => tag.Contains(CommonBotName, StringComparison.InvariantCultureIgnoreCase));
    }

    /// <summary>
    ///     Добавляет случайный эмоут к сообщению в чате Discord.
    /// </summary>
    /// <param name="context">Контекст чата Discord, содержащий данные о текущем сообщении.</param>
    /// <param name="emote">Эмоут, который нужно добавить к сообщению.</param>
    /// <returns>Задача, представляющая выполнение операции добавления реакции.</returns>
    private static async Task AddReactionAsync(DiscordChatContext context, IEmote emote)
    {
        if (context.UserMessage != null) await context.UserMessage.AddReactionAsync(emote).ConfigureAwait(false);
    }

    /// <summary>
    ///     Получает случайный эмоут из доступных эмоутов гильдии.
    /// </summary>
    /// <param name="context">Контекст чата Discord, содержащий данные о текущем сообщении и канале.</param>
    /// <returns>Случайный эмоут, если эмоуты доступны, иначе null.</returns>
    private GuildEmote? GetRandomEmoteAsync(DiscordChatContext context)
    {
        var guildChannel = context.UserMessage?.Channel as SocketGuildChannel;
        var emotes = guildChannel?.Guild.Emotes;

        if (emotes == null || emotes.Count <= 0) return null;
        var randomIndex = Random.Next(emotes.Count);
        return emotes.ElementAt(randomIndex);
    }

    /// <summary>
    ///     Случайным образом добавляет реакцию на сообщение в Discord.
    /// </summary>
    /// <param name="context">Контекст чата с сообщением.</param>
    /// <returns>Задача, представляющая выполнение асинхронной операции.</returns>
    private async Task RandomHandleMessageAsync(DiscordChatContext context)
    {
        try
        {
            if (ShouldAddRandomReaction())
            {
                var emote = GetRandomEmoteAsync(context);
                if (emote != null) await AddReactionAsync(context, emote).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при добавлении случайной реакции в Discord.");
        }
    }

    /// <summary>
    ///     Определяет, нужно ли добавить случайную реакцию на сообщение.
    ///     Вероятность определяется случайным числом.
    /// </summary>
    /// <returns>True, если реакцию нужно добавить, иначе False.</returns>
    private bool ShouldAddRandomReaction()
    {
        return Random.NextDouble() < RandomReactionProbability;
    }
}