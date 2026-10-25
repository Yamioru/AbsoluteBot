using System.Runtime.CompilerServices;
using AbsoluteBot.Chat.Context;
using AbsoluteBot.Events;
using AbsoluteBot.Services.ChatServices.Interfaces;
using AbsoluteBot.Services.UtilityServices;
using Serilog;
using TwitchLib.Api;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Interfaces;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Events;
using TwitchLib.Communication.Models;
using Stream = TwitchLib.Api.Helix.Models.Streams.GetStreams.Stream;

namespace AbsoluteBot.Services.ChatServices.TwitchChat;

/// <summary>
///     Реализует сервис для работы с Twitch, включая обработку сообщений, отправку сообщений,
///     создание клипов и работу с сокращением URL.
/// </summary>
public class TwitchChatService(ConfigService configService, UrlShortenerService urlShortenerService, ICensorshipService censorshipService,
        TwitchMessageHandler messageHandler, TwitchMessageDataProcessor messageDataProcessor, TwitchImageProcessor imageProcessor,
        StreamChatterStatsService streamChatterStatsService, ChatAchievementService chatAchievementService)
    : IChatService, IDisposable, IUrlShorteningService, IAsyncInitializable, IChatImageService
{
    private const int ReconnectDelayMilliseconds = 5000;
    public const int MaxMessageLength = 500;
    private const int MessagesAllowedInPeriod = 750;
    private const int ThrottlingPeriodSeconds = 30;
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private readonly TwitchMessageIdDeduplicator _messageIdDeduplicator = new();
    private readonly object _disposeLock = new();
    private int _isReconnecting;
    private bool _isConfigured;
    private bool _isDisposed;
    private ClientOptions? _clientOptions;
    private ConnectionCredentials? _credentials;
    private string? _broadcasterId;
    private string? _channelName;
    private TwitchAPI? _twitchApi;
    private TwitchClient? _twitchClient;

    /// <summary>
    ///     Асинхронная инициализация сервиса
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            _channelName = await configService.GetConfigValueAsync<string>("TwitchChannelName").ConfigureAwait(false);
            _broadcasterId = await configService.GetConfigValueAsync<string>("TwitchBroadcasterId").ConfigureAwait(false);
            var clientId = await configService.GetConfigValueAsync<string>("TwitchClientId").ConfigureAwait(false);
            var accessToken = await configService.GetConfigValueAsync<string>("TwitchAccessToken").ConfigureAwait(false);
            var botName = await configService.GetConfigValueAsync<string>("TwitchBotName").ConfigureAwait(false);
            var oAuthToken = await configService.GetConfigValueAsync<string>("TwitchOAuthToken").ConfigureAwait(false);
            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(botName) ||
                string.IsNullOrEmpty(oAuthToken))
            {
                Log.Warning("Ошибка при инициализации сервиса чата twitch, отсутствуют параметры авторизации.");
                return;
            }

            _credentials = new ConnectionCredentials(botName, oAuthToken);
            _twitchApi = new TwitchAPI
            {
                Settings = {ClientId = clientId, AccessToken = accessToken}
            };

            _clientOptions = new ClientOptions
            {
                MessagesAllowedInPeriod = MessagesAllowedInPeriod,
                ThrottlingPeriod = TimeSpan.FromSeconds(ThrottlingPeriodSeconds)
            };

            _isConfigured = true; // Конфигурация завершена
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при асинхронной инициализации TwitchChatService.");
            _isConfigured = false; // Если что-то пошло не так, сервис помечается как неконфигурированный
        }
    }

    /// <summary>
    ///     Асинхронно извлекает изображение в формате Base64 из сообщения Twitch, если оно существует.
    /// </summary>
    /// <param name="context">Контекст чата Twitch.</param>
    /// <param name="message">Полученное сообщение в чате.</param>
    /// <returns>Строка в формате Base64, представляющая изображение, или <c>null</c>, если изображение отсутствует.</returns>
    public async Task<string?> GetImageAsBase64Async(string message, ChatContext context)
    {
        return await imageProcessor.GetBase64ImageFromMessageAsync(message, context);
    }

    /// <summary>
    ///     Событие, которое вызывается при получении нового сообщения в чате.
    /// </summary>
    public event EventHandler<MessageReceivedEventArgs>? MessageReceived;

    /// <summary>
    ///     Подключает сервис к Twitch и инициализирует обработку сообщений.
    /// </summary>
    public async Task Connect()
    {
        if (!_isConfigured) return;
        await _connectLock.WaitAsync().ConfigureAwait(false);
        try
        {
            while (!_isDisposed)
                try
                {
                    ReplaceTwitchClient();
                    _twitchClient!.Connect();
                    Log.ForContext("ConnectionEvent", true).Information("Соединение с Twitch установлено.");
                    break;
                }
                catch (Exception ex)
                {
                    Log.ForContext("ConnectionEvent", true).Error(ex, "Ошибка подключения к Twitch. Повторная попытка.");
                    await Task.Delay(ReconnectDelayMilliseconds).ConfigureAwait(false);
                }
        }
        finally
        {
            _connectLock.Release();
        }
    }

    /// <summary>
    ///     Отправляет сообщение в Twitch-чат с учётом цензуры.
    /// </summary>
    /// <param name="message">Текст сообщения для отправки.</param>
    /// <param name="context">Контекст чата, содержащий информацию о канале и сообщении.</param>
    public Task SendMessageAsync(string message, ChatContext context)
    {
        return ExecuteIfServiceClientIsReady(async client =>
        {
            if (context is not TwitchChatContext twitchContext) return;
            var censoredMessage = censorshipService.ApplyCensorship(message, MaxMessageLength, true);
            client.SendReply(twitchContext.Channel, twitchContext.MessageId, censoredMessage);
            await Task.CompletedTask.ConfigureAwait(false);
        });
    }

    /// <summary>
    ///     Завершает работу сервиса и освобождает ресурсы.
    /// </summary>
    public void Dispose()
    {
        lock (_disposeLock)
        {
            if (_isDisposed) return;
            _isDisposed = true;
            if (_twitchClient != null)
            {
                UnsubscribeFromEvents(_twitchClient);
                _twitchClient.Disconnect();
                _twitchClient = null;
            }
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    ///     Асинхронно отправляет сокращенную ссылку в чат Twitch.
    /// </summary>
    /// <param name="url">Ссылка для сокращения.</param>
    /// <param name="context">Контекст чата, содержащий информацию о канале и сообщении.</param>
    public Task SendShortenedUrlAsync(string url, ChatContext context)
    {
        return ExecuteIfServiceClientIsReady(async client =>
        {
            if (context is not TwitchChatContext twitchContext) return;
            var shortUrl = await urlShortenerService.ShrinkUrlAsync(url).ConfigureAwait(false);
            shortUrl ??= "Картинка не найдена.";
            client.SendReply(twitchContext.Channel, twitchContext.MessageId, shortUrl);
        });
    }

    /// <summary>
    ///     Создает клип на основе текущего стрима на Twitch.
    /// </summary>
    /// <returns>True, если клип успешно создан, иначе False.</returns>
    public async Task<bool> ClipCreate()
    {
        try
        {
            if (_twitchApi == null || _isDisposed || !_isConfigured) return false;
            await _twitchApi.Helix.Clips.CreateClipAsync(_broadcasterId).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка создания клипа.");
            return false;
        }
    }

    /// <summary>
    ///     Асинхронно получает текущее состояние стрима на Twitch.
    /// </summary>
    /// <returns>Задача, возвращающая объект Stream с информацией о стриме, или null, если стрим не найден.</returns>
    public async Task<Stream?> GetStreamState()
    {
        if (_twitchApi == null || _isDisposed || !_isConfigured) return null;
        var userLogins = new List<string?> {_channelName};
        var result = await _twitchApi.Helix.Streams.GetStreamsAsync(userLogins: userLogins).ConfigureAwait(false);
        return result.Streams.FirstOrDefault();
    }

    /// <summary>
    ///     Выполняет указанное действие с клиентом Twitch, если клиент не равен null и сервис инициализирован.
    ///     Это гарантирует, что действия не будут выполняться, если клиент не инициализирован или сервис завершён.
    /// </summary>
    /// <param name="action">Функция, которая должна быть выполнена с клиентом Twitch.</param>
    /// <param name="methodName">Имя метода, вызвавшего выполнение этого действия. Используется для логирования.</param>
    /// <returns>Задача, представляющая выполнение действия с клиентом Twitch.</returns>
    private async Task ExecuteIfServiceClientIsReady(Func<TwitchClient, Task> action, [CallerMemberName] string methodName = "")
    {
        if (_twitchClient != null && !_isDisposed && _isConfigured)
            try
            {
                await action(_twitchClient).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка в методе {MethodName} при выполнении операции с Twitch _twitchClient.",
                    methodName);
            }
    }

    /// <summary>
    ///     Асинхронно обрабатывает событие получения сообщения в чате Twitch.
    /// </summary>
    /// <param name="sender">Источник события.</param>
    /// <param name="e">Аргументы события, содержащие данные сообщения.</param>
    private async void HandleMessageReceived(object? sender, OnMessageReceivedArgs e)
    {
        try
        {
            if (!_messageIdDeduplicator.TryTake(e.ChatMessage.Id)) return;
            if (!messageDataProcessor.TryParseValidMessage(e.ChatMessage, this, out var messageText, out var context)) return;
            await streamChatterStatsService.RecordMessageAsync(context.DisplayedName, context.Platform, context.UserId).ConfigureAwait(false);
            _ = chatAchievementService.TryRecordAsync(context.DisplayedName, messageText, "Twitch", context.UserId);
            var processedMessage = await messageHandler.HandleMessageAsync(messageText, context).ConfigureAwait(false);
            // Вызов события для дальнейшей обработки
            MessageReceived?.Invoke(this, new MessageReceivedEventArgs(processedMessage, context));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при обработке сообщения на Twitch.");
        }
    }

    /// <summary>
    ///     Обрабатывает событие отключения клиента от Twitch и инициирует переподключение.
    /// </summary>
    private async void OnClientDisconnected(object? sender, OnDisconnectedEventArgs e)
    {
        if (_isDisposed) return;
        if (sender is ITwitchClient disconnected && !ReferenceEquals(disconnected, _twitchClient)) return;
        if (Interlocked.CompareExchange(ref _isReconnecting, 1, 0) != 0) return;

        try
        {
            Log.ForContext("ConnectionEvent", true).Warning($"Соединение с Twitch потеряно. Причина: {e}. Попытка переподключения...");
            if (_twitchClient != null) UnsubscribeFromEvents(_twitchClient);
            await Task.Delay(ReconnectDelayMilliseconds).ConfigureAwait(false);
            if (_isDisposed) return;
            await Connect().ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Exchange(ref _isReconnecting, 0);
        }
    }

    /// <summary>
    ///     Обрабатывает событие подарочной подписки в чате Twitch и отправляет благодарственное сообщение.
    /// </summary>
    /// <param name="sender">Источник события.</param>
    /// <param name="e">Аргументы события, содержащие информацию о подарочной подписке.</param>
    private async void OnGiftedSubscription(object? sender, OnGiftedSubscriptionArgs e)
    {
        await SendEventMessageAsync(e.Channel,
                $"MercyWing1 DinoDance Спасибо за подарочные подписки {e.GiftedSubscription.Login} DinoDance MercyWing2")
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Асинхронно обрабатывает событие нового подписчика в чате Twitch.
    /// </summary>
    /// <param name="sender">Источник события.</param>
    /// <param name="e">Аргументы события, содержащие данные о новом подписчике.</param>
    private async void OnNewSubscriber(object? sender, OnNewSubscriberArgs e)
    {
        await SendEventMessageAsync(e.Channel, $"MercyWing1 DinoDance Спасибо за подписку {e.Subscriber.Login} DinoDance MercyWing2")
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Асинхронно обрабатывает событие рейда в чате Twitch.
    /// </summary>
    /// <param name="sender">Источник события.</param>
    /// <param name="e">Аргументы события, содержащие данные о рейде.</param>
    private async void OnRaid(object? sender, OnRaidNotificationArgs e)
    {
        await SendEventMessageAsync(e.Channel, $"MercyWing1 DinoDance Спасибо за рейд {e.RaidNotification.Login} DinoDance MercyWing2 +250")
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Обрабатывает событие повторной подписки пользователя на канал Twitch.
    /// </summary>
    /// <param name="sender">Источник события.</param>
    /// <param name="e">Данные события, включая информацию о повторной подписке.</param>
    private async void OnReSubscriber(object? sender, OnReSubscriberArgs e)
    {
        await SendEventMessageAsync(e.Channel, $"MercyWing1 DinoDance Спасибо за переподписку {e.ReSubscriber.Login} DinoDance MercyWing2")
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Обрабатывает событие блокировки пользователя (бан) в чате Twitch.
    /// </summary>
    /// <param name="sender">Источник события.</param>
    /// <param name="e">Данные события, включая информацию о забаненном пользователе.</param>
    private async void OnUserBanned(object? sender, OnUserBannedArgs e)
    {
        await SendEventMessageAsync(e.UserBan.Channel, "Погиб поэт! — невольник чести.").ConfigureAwait(false);
    }

    /// <summary>
    ///     Обрабатывает событие таймаута (временного бана) пользователя на Twitch.
    /// </summary>
    /// <param name="sender">Источник события.</param>
    /// <param name="e">Данные события, включая информацию о пользователе и причине таймаута.</param>
    private async void OnUserTimeout(object? sender, OnUserTimedoutArgs e)
    {
        await SendEventMessageAsync(e.UserTimeout.Channel, "Сиди за решеткой в темнице сырой!").ConfigureAwait(false);
    }

    /// <summary>
    ///     Асинхронно отправляет сообщение в указанный канал Twitch.
    /// </summary>
    /// <param name="channel">Название канала, в который нужно отправить сообщение.</param>
    /// <param name="message">Текст сообщения, которое нужно отправить.</param>
    /// <returns>Задача, представляющая выполнение операции отправки сообщения.</returns>
    private Task SendEventMessageAsync(string channel, string message)
    {
        return ExecuteIfServiceClientIsReady(client =>
        {
            client.SendMessage(channel, message);
            return Task.CompletedTask;
        });
    }

    /// <summary>
    ///     Отключает предыдущий Twitch-клиент и создаёт новый, чтобы не держать два IRC-подключения одновременно.
    /// </summary>
    private void ReplaceTwitchClient()
    {
        var previous = _twitchClient;
        if (previous != null)
        {
            UnsubscribeFromEvents(previous);
            try
            {
                previous.Disconnect();
            }
            catch (Exception ex)
            {
                Log.ForContext("ConnectionEvent", true).Warning(ex, "Ошибка при отключении предыдущего Twitch-клиента.");
            }
        }

        var customClient = new WebSocketClient(_clientOptions);
        _twitchClient = new TwitchClient(customClient);
        _twitchClient.Initialize(_credentials, _channelName);
        SubscribeToEvents(_twitchClient);
    }

    /// <summary>
    ///     Подписывает на события клиента Twitch, такие как получение сообщений, новые подписчики и рейды.
    /// </summary>
    /// <param name="client">Клиент Twitch, к событиям которого необходимо подписаться.</param>
    private void SubscribeToEvents(ITwitchClient client)
    {
        client.OnMessageReceived += HandleMessageReceived;
        client.OnNewSubscriber += OnNewSubscriber;
        client.OnReSubscriber += OnReSubscriber;
        client.OnGiftedSubscription += OnGiftedSubscription;
        client.OnUserBanned += OnUserBanned;
        client.OnUserTimedout += OnUserTimeout;
        client.OnRaidNotification += OnRaid;
        client.OnDisconnected += OnClientDisconnected;
    }

    /// <summary>
    ///     Отписывает от событий клиента Twitch.
    /// </summary>
    /// <param name="client">Клиент Twitch, от событий которого необходимо отписаться.</param>
    private void UnsubscribeFromEvents(ITwitchClient client)
    {
        client.OnMessageReceived -= HandleMessageReceived;
        client.OnDisconnected -= OnClientDisconnected;
        client.OnNewSubscriber -= OnNewSubscriber;
        client.OnReSubscriber -= OnReSubscriber;
        client.OnGiftedSubscription -= OnGiftedSubscription;
        client.OnUserBanned -= OnUserBanned;
        client.OnUserTimedout -= OnUserTimeout;
        client.OnRaidNotification -= OnRaid;
    }
}