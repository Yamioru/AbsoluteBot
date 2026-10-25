using System.Collections.Concurrent;
using AbsoluteBot.Chat.Context;
using AbsoluteBot.Services.UtilityServices;
using Serilog;

namespace AbsoluteBot.Services.ChatServices.TelegramChat;

/// <summary>
///     Класс для управления идентификаторами каналов и их типами в Telegram.
///     Отвечает за кэширование и получение данных о каналах.
/// </summary>
/// <remarks>
///     Класс для управления идентификаторами каналов и их типами в Telegram.
///     Отвечает за кэширование и получение данных о каналах.
/// </remarks>
public class TelegramChannelManager(ConfigService configService) : IAsyncInitializable
{
    private readonly ConcurrentDictionary<ChannelType, long> _channelIdCache = new();

    public async Task InitializeAsync()
    {
        await LoadChannelIdsIntoCacheAsync();
    }

    /// <summary>
    ///     Определяет тип канала на основе его идентификатора.
    ///     Возвращает Premium, Administrative или General.
    /// </summary>
    /// <param name="channelId">Идентификатор канала.</param>
    /// <returns>Тип канала.</returns>
    public async Task<ChannelType> DetermineChannelType(long channelId)
    {
        if (channelId == await GetTelegramChannelId(ChannelType.Premium).ConfigureAwait(false))
            return ChannelType.Premium;
        if (channelId == await GetTelegramChannelId(ChannelType.Administrative).ConfigureAwait(false))
            return ChannelType.Administrative;
        if (channelId == await GetTelegramChannelId(ChannelType.Announce).ConfigureAwait(false))
            return ChannelType.Announce;

        return ChannelType.General;
    }

    /// <summary>
    ///     Возвращает идентификатор канала для указанного типа канала с использованием кэширования.
    /// </summary>
    /// <param name="channelType">Тип канала, для которого нужно получить идентификатор.</param>
    /// <returns>Идентификатор канала.</returns>
    public async Task<long> GetTelegramChannelId(ChannelType channelType)
    {
        // Проверка, есть ли идентификатор в кэше
        if (_channelIdCache.TryGetValue(channelType, out var cachedValue))
            return cachedValue;

        // Если нет в кэше, загружается из конфигурации
        var key = $"TelegramChannelIds:{channelType}";
        var channelId = await configService.GetConfigValueAsync<long>(key).ConfigureAwait(false);

        // Сохранение значения в кэш
        _channelIdCache[channelType] = channelId;
        return channelId;
    }

    /// <summary>
    ///     Загружает идентификаторы всех каналов в кэш.
    /// </summary>
    private async Task LoadChannelIdsIntoCacheAsync()
    {
        foreach (ChannelType channelType in Enum.GetValues(typeof(ChannelType)))
        {
            // Загрузка идентификаторов каналов в кэш
            var key = $"TelegramChannelIds:{channelType}";
            var channelId = await configService.GetConfigValueAsync<long>(key).ConfigureAwait(false);

            // Если идентификатор не задан, инициализация значением по умолчанию
            if (channelId == 0)
            {
                Log.Warning($"Идентификатор канала {channelType} не настроен, используйте корректное значение.");
                _channelIdCache[channelType] = 0;
            }
            else
            {
                _channelIdCache[channelType] = channelId;
            }
        }
    }
}