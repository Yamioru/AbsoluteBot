using AbsoluteBot.Chat.Commands;
using AbsoluteBot.Chat.Commands.Registry;
using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services.ChatServices.Discord;
using AbsoluteBot.Services.ChatServices.TelegramChat;
using AbsoluteBot.Services.ChatServices.TwitchChat;
using AbsoluteBot.Services.ChatServices.VkPlayLive;
using AbsoluteBot.Services.GoogleSheetsServices;
using AbsoluteBot.Services.NeuralNetworkServices;
using AbsoluteBot.Services.UtilityServices;
using Serilog;
using Stream = TwitchLib.Api.Helix.Models.Streams.GetStreams.Stream;

namespace AbsoluteBot.Services.ScheduledTasks;
#pragma warning disable IDE0028
/// <summary>
///     Сервис для мониторинга стримов на Twitch и обновления информации о стриме.
/// </summary>
public class TwitchStreamMonitoringService(TwitchChatService twitchChatService, ConfigService configService,
    TelegramChatService telegramChatService, DiscordChatService discordChatService, INeuralChatService neuralChat,
    TelegramChannelManager telegramChannelManager,
    GameGoogleSheetsService gameGoogleSheetsService, StreamGoogleSheetsService streamGoogleSheetsService,
    GameProgressService gameProgressService, VkPlayChatService vkPlayChatService, ICommandRegistry commandRegistry,
    StreamChatterStatsService streamChatterStatsService) : IAsyncInitializable
{
    private const int VkPlayAllCommandsReminderIntervalHours = 3;
    private const int VkPlayRandomCommandReminderIntervalMinutes = 42;
    private const int VkPlayHighlightsReminderIntervalMinutes = 128;
    private const string VkPlayReminderMessage = "Хэй молодёжь, блин, привет друзья, напишите !команды, чтоб узнать кто я. А если вдруг скучно, то тегни меня и напиши ты как дела!";
    private const string VkPlayHighlightsReminderMessage = "А вы знали, что существует канал с хайлайтами со стримов? https://www.youtube.com/@victimcourtHighlights";
    private bool _streamWasOnline;
    private CancellationTokenSource? _vkPlayAllCommandsReminderCancellationTokenSource;
    private CancellationTokenSource? _vkPlayRandomCommandReminderCancellationTokenSource;
    private CancellationTokenSource? _vkPlayHighlightsReminderCancellationTokenSource;
    private List<string>? _streamLinks = new();
    private string? _lastGameName;

    public async Task InitializeAsync()
    {
        _streamLinks = await configService.GetConfigValueAsync<List<string>>("StreamLinks").ConfigureAwait(false);
        _streamWasOnline = await configService.GetConfigValueAsync<bool>("StreamWasOnline").ConfigureAwait(false);
        _lastGameName = await configService.GetConfigValueAsync<string>("LastGameName").ConfigureAwait(false);
        if (string.IsNullOrEmpty(_lastGameName) || _streamLinks == null || _streamLinks.Count < 1)
            Log.Warning("Не удалось загрузить последнюю игру на стриме или ссылки на стрим.");
        if (_streamWasOnline)
        {
            _ = StartVkPlayAllCommandsReminderAsync().ConfigureAwait(false);
            _ = StartVkPlayRandomCommandReminderAsync().ConfigureAwait(false);
            _ = StartVkPlayHighlightsReminderAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Выполняет проверку состояния стрима на Twitch и обрабатывает изменения.
    /// </summary>
    public async Task ExecuteTwitchOnlineCheckTask(int twitchCheckTimeMinutes)
    {
        try
        {
            var stream = await twitchChatService.GetStreamState().ConfigureAwait(false);
            if (stream == null)
                await HandleStreamEnded().ConfigureAwait(false);
            else
                await HandleStreamStartedOrContinued(stream, twitchCheckTimeMinutes).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка проверки состояния стрима на Twitch.");
        }
    }

    /// <summary>
    ///     Обрабатывает смену игры на стриме.
    /// </summary>
    /// <param name="newGameName">Название новой игры.</param>
    private async Task HandleGameChange(string newGameName)
    {
        var streamNumber = await configService.GetConfigValueAsync<int>("StreamNumber").ConfigureAwait(false);
        await UpdateGoogleSheetsForStreamStartAsync(newGameName, streamNumber).ConfigureAwait(false);
        _lastGameName = newGameName;
    }

    /// <summary>
    ///     Обрабатывает завершение стрима.
    /// </summary>
    private async Task HandleStreamEnded()
    {
        if (!_streamWasOnline) return;

        _streamWasOnline = false;
        await configService.SetConfigValueAsync("StreamWasOnline", false).ConfigureAwait(false);

        // Остановка таймера VkPlay при завершении стрима
        StopVkPlayReminder();

        // Отправка уведомления в Telegram о завершении стрима
        var telegramChatId = await telegramChannelManager.GetTelegramChannelId(ChannelType.Announce).ConfigureAwait(false);
        await telegramChatService.SendMessageToChannelAsync("Стрим окончен!", telegramChatId.ToString()).ConfigureAwait(false);
        await telegramChatService.UnPinLastMessage(telegramChatId.ToString()).ConfigureAwait(false);

        await neuralChat.AddUserMessageToChatHistory("Стрим завершился!", "System").ConfigureAwait(false);
    }

    /// <summary>
    ///     Обрабатывает начало стрима на Twitch.
    /// </summary>
    /// <param name="stream">Информация о текущем стриме.</param>
    private async Task HandleStreamStarted(Stream stream)
    {
        _streamWasOnline = true;
        await configService.SetConfigValueAsync("StreamWasOnline", true).ConfigureAwait(false);

        var streamNumber = await configService.GetConfigValueAsync<int>("StreamNumber").ConfigureAwait(false);
        streamNumber++;
        await streamChatterStatsService.BeginNewStreamAsync(streamNumber).ConfigureAwait(false);
        await configService.SetConfigValueAsync("StreamNumber", streamNumber).ConfigureAwait(false);

        // Запуск асинхронных задач для отправки информации о доступных командах и канале хайлайтов
        _ = StartVkPlayAllCommandsReminderAsync().ConfigureAwait(false);
        _ = StartVkPlayRandomCommandReminderAsync().ConfigureAwait(false);
        _ = StartVkPlayHighlightsReminderAsync().ConfigureAwait(false);

        // Уведомление о начале стрима в Telegram и Discord
        if (_streamLinks != null)
        {
            var announceMessage = "Стрим включен!\n" + string.Join("\n", _streamLinks);
            var telegramChatId = await telegramChannelManager.GetTelegramChannelId(ChannelType.Announce).ConfigureAwait(false);
            await telegramChatService.SendPinnedMessageToChannelAsync(announceMessage, telegramChatId.ToString()).ConfigureAwait(false);
            await discordChatService.AnnounceMessage(announceMessage).ConfigureAwait(false);
        }

        // Обновление таблицы игр
        await UpdateGoogleSheetsForStreamStartAsync(stream.GameName, streamNumber).ConfigureAwait(false);

        // Уведомление самого бота о начале стрима
        await neuralChat.AddUserMessageToChatHistory($"Начался стрим, игра на стриме: {stream.GameName}!", "System").ConfigureAwait(false);
    }

    /// <summary>
    ///     Обрабатывает состояние стрима при его продолжении или смене игры.
    /// </summary>
    /// <param name="stream">Информация о текущем стриме.</param>
    /// <param name="twitchCheckTimeMinutes">Время в минутах, которое проходит между вызовами обновления прогресса.</param>
    private async Task HandleStreamStartedOrContinued(Stream stream, int twitchCheckTimeMinutes)
    {
        if (!_streamWasOnline)
            await HandleStreamStarted(stream).ConfigureAwait(false);
        else if (_lastGameName != stream.GameName)
            await HandleGameChange(stream.GameName).ConfigureAwait(false);

        // Обновление прогресса игры
        await UpdateGameProgress(stream.GameName, twitchCheckTimeMinutes).ConfigureAwait(false);
    }

    /// <summary>
    ///     Запускает повторяющееся отправление напоминаний в чат VkPlay с интервалом в 2 часа.
    /// </summary>
    private async Task StartVkPlayAllCommandsReminderAsync()
    {
        _vkPlayAllCommandsReminderCancellationTokenSource = new CancellationTokenSource();

        try
        {
            // Пока стрим активен, отправляем сообщение каждые 2 часа
            while (!_vkPlayAllCommandsReminderCancellationTokenSource.Token.IsCancellationRequested)
            {
                // Ожидание 2 часа или отмены через CancellationToken
                await Task.Delay(TimeSpan.FromHours(VkPlayAllCommandsReminderIntervalHours), _vkPlayAllCommandsReminderCancellationTokenSource.Token)
                    .ConfigureAwait(false);
                await vkPlayChatService.SendMessageToChannelAsync(VkPlayReminderMessage).ConfigureAwait(false);
            }
        }
        catch (TaskCanceledException)
        {
            Log.Information("Отправка сообщений на VkPlay была отменена.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при отправке напоминания на VkPlay.");
        }
    }

    /// <summary>
    ///     Запускает повторяющееся отправление напоминаний в чат VkPlay с интервалом в 40 минут.
    /// </summary>
    private async Task StartVkPlayRandomCommandReminderAsync()
    {
        _vkPlayRandomCommandReminderCancellationTokenSource = new CancellationTokenSource();
        var allCommands = commandRegistry.GetAllCommands();
        var rnd = new Random();
        ChatContext chatContext = new VkPlayChatContext(string.Empty, 0, vkPlayChatService, null, null, new List<string>(), 0, new List<string>());
        var baseCommand = new ParsedCommand(string.Empty, string.Empty, chatContext, UserRole.Default);
        var randomSortedPublicCommands = allCommands.Where(c => c.CanExecute(baseCommand)).OrderBy(_ => rnd.Next()).ToList();
        try
        {
            // Пока стрим активен, отправляем сообщение каждые 40 минут
            foreach (var command in randomSortedPublicCommands)
            {
                if (_vkPlayRandomCommandReminderCancellationTokenSource.Token.IsCancellationRequested) continue;
                var parameters = string.Empty;
                if (command is IParameterized parameterizedCommand)
                    parameters = parameterizedCommand.Parameters != null ? "*" + parameterizedCommand.Parameters + "*" : "*пусто*";
                var message = $"А вы знали, что есть команда \"{command.Name} {parameters}\", которая {command.Description}";
                // Ожидание 40 минут или отмены через CancellationToken
                await Task.Delay(TimeSpan.FromMinutes(VkPlayRandomCommandReminderIntervalMinutes),
                    _vkPlayRandomCommandReminderCancellationTokenSource.Token).ConfigureAwait(false);
                await vkPlayChatService.SendMessageToChannelAsync(message).ConfigureAwait(false);
            }
        }
        catch (TaskCanceledException)
        {
            Log.Information("Отправка сообщений на VkPlay была отменена.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при отправке напоминания на VkPlay.");
        }
    }

    /// <summary>
    ///     Запускает повторяющееся отправление напоминания о канале хайлайтов в чат VkPlay с интервалом в 128 минут.
    /// </summary>
    private async Task StartVkPlayHighlightsReminderAsync()
    {
        _vkPlayHighlightsReminderCancellationTokenSource = new CancellationTokenSource();

        try
        {
            while (!_vkPlayHighlightsReminderCancellationTokenSource.Token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(VkPlayHighlightsReminderIntervalMinutes),
                    _vkPlayHighlightsReminderCancellationTokenSource.Token).ConfigureAwait(false);
                await vkPlayChatService.SendMessageToChannelAsync(VkPlayHighlightsReminderMessage).ConfigureAwait(false);
            }
        }
        catch (TaskCanceledException)
        {
            Log.Information("Отправка сообщений на VkPlay была отменена.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при отправке напоминания о хайлайтах на VkPlay.");
        }
    }

    /// <summary>
    ///     Останавливает отправление сообщений в VkPlay.
    /// </summary>
    private void StopVkPlayReminder()
    {
        _vkPlayAllCommandsReminderCancellationTokenSource?.Cancel();
        _vkPlayRandomCommandReminderCancellationTokenSource?.Cancel();
        _vkPlayHighlightsReminderCancellationTokenSource?.Cancel();
    }

    /// <summary>
    ///     Обновляет прогресс игры, используя данные стрима.
    /// </summary>
    /// <param name="gameName">Название игры.</param>
    /// <param name="twitchCheckTimeMinutes">Время в минутах, которое проходит между вызовами обновления прогресса.</param>
    private async Task UpdateGameProgress(string gameName, int twitchCheckTimeMinutes)
    {
        await gameProgressService.UpdateLastGameNameAsync(gameName).ConfigureAwait(false);
        await gameProgressService.AddTimeToCurrentGameAsync(gameName, twitchCheckTimeMinutes).ConfigureAwait(false);
        await gameProgressService.CalculateGameProgressAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     Обновляет данные в Google Sheets, связанные с началом нового стрима.
    /// </summary>
    private async Task UpdateGoogleSheetsForStreamStartAsync(string gameName, int streamNumber)
    {
        // Проверяется и добавляется игра в таблицу игр, если её там еще нет
        await gameGoogleSheetsService.AddOrUpdateGameWithStreamAsync(gameName, streamNumber).ConfigureAwait(false);

        // Добавляется новая строку стрима в таблицу стримов
        await streamGoogleSheetsService.AddOrUpdateStreamRowAsync(streamNumber, gameName).ConfigureAwait(false);
    }
}