using AbsoluteBot.Chat.Context;
using Discord.WebSocket;

namespace AbsoluteBot.Services.ChatServices.Discord;

/// <summary>
///     Сервис для отправки уведомлений и упоминаний пользователей в каналах Discord.
/// </summary>
public class DiscordNotificationService(DiscordGuildChannelService guildChannelService, DiscordMessageSenderService messageSenderService,
    DiscordSocketClient? client)
{
    /// <summary>
    ///     Делает объявление в канале оповещений Discord.
    /// </summary>
    /// <param name="message">Текст объявления.</param>
    public async Task AnnounceMessageAsync(string message)
    {
        if (client == null) return;

        var (guildId, chatId) = await GetGuildAndChannelIds(ChannelType.Announce, ChannelType.Announce).ConfigureAwait(false);
        if (guildId == 0 || chatId == 0) return;

        await SendGuildAnnouncement(guildId, chatId, message).ConfigureAwait(false);
    }

    /// <summary>
    ///     Призывает пользователя с упоминанием в указанном канале.
    /// </summary>
    /// <param name="username">Имя пользователя, который призывает.</param>
    /// <param name="usernameToCall">Имя пользователя, которого нужно призвать.</param>
    /// <returns>Результат призыва пользователя.</returns>
    public async Task<string> SummonUser(string username, string usernameToCall)
    {
        if (client == null) return "Нет подключения к Discord.";

        var (guildId, chatId) = await GetGuildAndChannelIds(ChannelType.Premium, ChannelType.Premium).ConfigureAwait(false);
        if (guildId == 0 || chatId == 0) return "Не хватает маны на призыв.";

        var user = await FindUserInGuild(guildId, usernameToCall).ConfigureAwait(false);
        if (user == null) return "Пользователь не найден.";

        await messageSenderService.SendMessageToChannelAsync(chatId, $"{user.Mention}, тебя зовёт {username}.").ConfigureAwait(false);
        return "Призыв осуществлён.";
    }

    /// <summary>
    ///     Ищет пользователя в указанной гильдии по его имени или никнейму.
    /// </summary>
    /// <param name="guildId">Идентификатор гильдии, в которой будет производиться поиск.</param>
    /// <param name="usernameToCall">Имя или никнейм пользователя, которого нужно найти.</param>
    /// <returns>Объект <see cref="SocketGuildUser" />, представляющий пользователя, или null, если пользователь не найден.</returns>
    private async Task<SocketGuildUser?> FindUserInGuild(ulong guildId, string usernameToCall)
    {
        var guild = client?.GetGuild(guildId);
        if (guild == null) return null;

        await guild.DownloadUsersAsync().ConfigureAwait(false);
        return guild.Users.FirstOrDefault(user =>
            user.Nickname?.Contains(usernameToCall, StringComparison.OrdinalIgnoreCase) ??
            user.Username.Contains(usernameToCall, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Получает идентификаторы гильдии и канала на основе указанных типов гильдии и канала.
    /// </summary>
    /// <param name="guildType">Тип гильдии для поиска идентификатора.</param>
    /// <param name="channelType">Тип канала для поиска идентификатора.</param>
    /// <returns>Кортеж с идентификаторами гильдии и канала.</returns>
    private async Task<(ulong guildId, ulong chatId)> GetGuildAndChannelIds(ChannelType guildType, ChannelType channelType)
    {
        var guildId = await guildChannelService.GetDiscordGuildId(guildType).ConfigureAwait(false);
        var chatId = await guildChannelService.GetDiscordChatId(channelType).ConfigureAwait(false);
        return (guildId, chatId);
    }

    /// <summary>
    ///     Отправляет сообщение с объявлением в указанный канал гильдии Discord.
    /// </summary>
    /// <param name="guildId">Идентификатор гильдии, в которой будет отправлено сообщение.</param>
    /// <param name="chatId">Идентификатор канала, в который будет отправлено сообщение.</param>
    /// <param name="message">Текст сообщения для отправки.</param>
    private async Task SendGuildAnnouncement(ulong guildId, ulong chatId, string message)
    {
        var guild = client?.GetGuild(guildId);
        if (guild == null) return;

        var announcementMessage = $"{guild.EveryoneRole.Mention} {message}";
        await messageSenderService.SendMessageToChannelAsync(chatId, announcementMessage).ConfigureAwait(false);
    }
}