using System.Collections.Concurrent;
using AbsoluteBot.Chat.Context;
using AbsoluteBot.Services.UtilityServices;
using Serilog;

namespace AbsoluteBot.Services.ChatServices.Discord;

/// <summary>
///     Сервис для управления гильдиями и чата Discord, предоставляющий функции для получения идентификаторов гильдий и
///     чатов.
/// </summary>
public class DiscordGuildChannelService(ConfigService configService) : IAsyncInitializable
{
    private readonly ConcurrentDictionary<ChannelType, ulong> _chatIdCache = new();
    private readonly ConcurrentDictionary<ChannelType, ulong> _guildIdCache = new();

    public async Task InitializeAsync()
    {
        await LoadGuildAndChatIdsIntoCacheAsync();
    }

    /// <summary>
    ///     Определяет тип чата по его идентификатору.
    /// </summary>
    /// <param name="chatId">Идентификатор чата.</param>
    /// <returns>Тип чата.</returns>
    public async Task<ChannelType> DetermineChatType(ulong chatId)
    {
        if (chatId == await GetDiscordChatId(ChannelType.Premium).ConfigureAwait(false)) return ChannelType.Premium;
        if (chatId == await GetDiscordChatId(ChannelType.Administrative).ConfigureAwait(false)) return ChannelType.Administrative;
        if (chatId == await GetDiscordChatId(ChannelType.Announce).ConfigureAwait(false)) return ChannelType.Announce;
        return ChannelType.General;
    }

    /// <summary>
    ///     Определяет тип гильдии по её идентификатору.
    /// </summary>
    /// <param name="guildId">Идентификатор гильдии.</param>
    /// <returns>Тип гильдии.</returns>
    public async Task<ChannelType> DetermineGuildType(ulong guildId)
    {
        if (guildId == await GetDiscordGuildId(ChannelType.Premium).ConfigureAwait(false))
            return ChannelType.Premium;
        if (guildId == await GetDiscordGuildId(ChannelType.Announce).ConfigureAwait(false))
            return ChannelType.Announce;
        if (guildId == await GetDiscordGuildId(ChannelType.Administrative).ConfigureAwait(false))
            return ChannelType.Administrative;
        return ChannelType.General;
    }

    /// <summary>
    ///     Возвращает идентификатор чата Discord для указанного типа чата.
    /// </summary>
    /// <param name="channelType">Тип чата.</param>
    /// <returns>Идентификатор чата.</returns>
    public async Task<ulong> GetDiscordChatId(ChannelType channelType)
    {
        if (_chatIdCache.TryGetValue(channelType, out var cachedValue)) return cachedValue;

        var key = $"DiscordChatIds:{channelType}";
        var value = await configService.GetConfigValueAsync<ulong>(key).ConfigureAwait(false);

        _chatIdCache[channelType] = value;
        return value;
    }

    /// <summary>
    ///     Возвращает идентификатор гильдии Discord для указанного типа гильдии.
    /// </summary>
    /// <param name="channelType">Тип канала (гильдии).</param>
    /// <returns>Идентификатор гильдии.</returns>
    public async Task<ulong> GetDiscordGuildId(ChannelType channelType)
    {
        if (_guildIdCache.TryGetValue(channelType, out var cachedValue)) return cachedValue;

        var key = $"DiscordGuildIds:{channelType}";
        var value = await configService.GetConfigValueAsync<ulong>(key).ConfigureAwait(false);

        _guildIdCache[channelType] = value;
        return value;
    }

    /// <summary>
    /// Загружает идентификаторы всех чатов и гильдий в кэш.
    /// </summary>
    private async Task LoadGuildAndChatIdsIntoCacheAsync()
    {
        foreach (ChannelType channelType in Enum.GetValues(typeof(ChannelType)))
        {
            // Загрузка идентификаторов чатов в кэш
            var chatKey = $"DiscordChatIds:{channelType}";
            var chatId = await configService.GetConfigValueAsync<ulong>(chatKey).ConfigureAwait(false);

            // Если идентификатор не задан, инициализация значением по умолчанию
            if (chatId == 0)
            {
                Log.Warning($"Идентификатор {channelType} чата в Discord не настроен, используйте корректное значение.");
                _chatIdCache[channelType] = 0;
            }
            else
            {
                _chatIdCache[channelType] = chatId;
            }

            // Загрузка идентификаторов гильдий в кэш
            var guildKey = $"DiscordGuildIds:{channelType}";
            var guildId = await configService.GetConfigValueAsync<ulong>(guildKey).ConfigureAwait(false);

            // Если идентификатор не задан, инициализация значением по умолчанию
            if (guildId == 0)
            {
                Log.Warning($"Идентификатор {channelType} гильдии в Discord не настроен, используйте корректное значение.");
                _guildIdCache[channelType] = 0;
            }
            else
            {
                _guildIdCache[channelType] = guildId;
            }
        }
    }
}