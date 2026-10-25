using AbsoluteBot.Chat.Context;

namespace AbsoluteBot.Chat.Commands;

/// <summary>
///     Предоставляет методы для проверки принадлежности сообщения различным типам источников.
/// </summary>
public static class CommandPermissionChecker
{
    /// <summary>
    ///     Проверяет, является ли чат административным для выполнения команды.
    ///     Административными считаются Administrative каналы в Telegram и Administrative чаты в Premium гильдии в Discord.
    /// </summary>
    /// <param name="command">Команда, которую нужно проверить.</param>
    /// <returns>Возвращает <c>true</c>, если это административный чат, иначе <c>false</c>.</returns>
    public static bool IsAdministrativeChannel(ParsedCommand command)
    {
        return command.Context switch
        {
            TelegramChatContext {ChannelType: ChannelType.Administrative} or DiscordChatContext
            {
                GuildType: ChannelType.Premium, ChatType: ChannelType.Administrative
            } => true,
            _ => false
        };
    }

    /// <summary>
    ///     Проверяет, является ли чат официальным для выполнения команды.
    ///     Официальными считаются Premium и Administrative каналы в Telegram, Premium гильдии в Discord и любые другие чаты на
    ///     других платформах.
    /// </summary>
    /// <param name="command">Команда, которую нужно проверить.</param>
    /// <returns>Возвращает <c>true</c>, если это официальный чат, иначе <c>false</c>.</returns>
    public static bool IsOfficialChannel(ParsedCommand command)
    {
        return command.Context switch
        {
            TelegramChatContext {ChannelType: ChannelType.Premium or ChannelType.Administrative} => true,
            TelegramChatContext => false,
            DiscordChatContext {GuildType: ChannelType.Premium} => true,
            DiscordChatContext => false,
            _ => true
        };
    }

    /// <summary>
    ///     Проверяет, является ли чат чатом стримингового сервиса (Twitch, VkPlayLive).
    /// </summary>
    /// <param name="command">Команда, которую нужно проверить.</param>
    /// <returns>Возвращает <c>true</c>, если это чат стримингового сервиса, иначе <c>false</c>.</returns>
    public static bool IsStreamingChannel(ParsedCommand command)
    {
        return command.Context switch
        {
            TwitchChatContext or VkPlayChatContext => true,
            _ => false
        };
    }
}