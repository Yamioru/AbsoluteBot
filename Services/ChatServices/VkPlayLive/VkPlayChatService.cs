using System.Runtime.CompilerServices;
using AbsoluteBot.Chat.Context;
using AbsoluteBot.Events;
using AbsoluteBot.Services.ChatServices.Interfaces;
using AbsoluteBot.Services.UtilityServices;
using Serilog;

namespace AbsoluteBot.Services.ChatServices.VkPlayLive;

/// <summary>
///     Реализует сервис для работы с VkPlayLive, включая подключение, отправку сообщений,
///     сокращение URL и обработку цензуры сообщений.
/// </summary>
public class VkPlayChatService(ConfigService configService, ICensorshipService censorshipService, UrlShortenerService urlShortenerService,
        VkPlayMessageSender messageSender, VkPlayMessageHandler messageHandler, VkPlayMessageDataProcessor messageProcessor,
        VkPlayImageProcessor imageProcessor, StreamChatterStatsService streamChatterStatsService, VkPlayAuthService authService,
        ChatAchievementService chatAchievementService)
    : IChatService, IUrlShorteningService, IAsyncDisposable, IAsyncInitializable, IChatImageService
{
    public const int MaxMessageLength = 500;
    private bool _isConfigured;
    private bool _isDisposed;
    private VkPlayConnectionManager? _connectionManager;
    private VkPlayMessageReceiver? _messageReceiver;

    /// <summary>
    ///     Освобождает ресурсы и завершает работу с VkPlayChatService.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        await _connectionManager!.DisconnectAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Инициализирует необходимые сервисы для работы VkPlayChatService.
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            var webSocketManager = new WebSocketConnectionManager();
            _connectionManager = CreateConnectionManager(configService, webSocketManager, authService);
            if (!await _connectionManager.InitializeAsync().ConfigureAwait(false)) return;
            _messageReceiver = CreateMessageReceiver(webSocketManager, _connectionManager);
            _messageReceiver.OnMessageReceived += async (_, message) => await ProcessReceivedMessageAsync(message).ConfigureAwait(false);
            _isConfigured = true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при инициализации VkPlayChatService;");
            _isConfigured = false;
        }
    }

    public async Task<string?> GetImageAsBase64Async(string message, ChatContext context)
    {
        if (context is not VkPlayChatContext chatContext) return null;
        return await imageProcessor.GetBase64ImageFromUrlsAsync(chatContext).ConfigureAwait(false);
    }

    public event EventHandler<MessageReceivedEventArgs>? MessageReceived;

    /// <summary>
    ///     Подключает чат-сервис к VkPlayLive и начинает получать сообщения.
    /// </summary>
    public Task Connect()
    {
        return ExecuteIfConfigured(async () =>
        {
            await _connectionManager!.ConnectAsync().ConfigureAwait(false);
            _messageReceiver!.StartReceivingMessages();
            Log.ForContext("ConnectionEvent", true).Information("Подключение к VkPlay произошло успешно.");
        });
    }

    /// <summary>
    ///     Асинхронно отправляет сообщение в чат VkPlayLive с учетом цензуры текста, отвечай на сообщение пользователя.
    /// </summary>
    /// <param name="message">Текст сообщения для отправки.</param>
    /// <param name="context">Контекст чата, содержащий информацию о текущем канале, пользователе и его сообщении.</param>
    public Task SendMessageAsync(string message, ChatContext context)
    {
        return ExecuteIfConfigured(async () =>
        {
            if (context is not VkPlayChatContext vkPlayChatContext) return;
            // Применение цензуры к сообщению
            var censoredMessage = censorshipService.ApplyCensorship(message, MaxMessageLength, true);
            await messageSender.PostMessageAsync(censoredMessage, vkPlayChatContext.MessageId).ConfigureAwait(false);
        });
    }

    /// <summary>
    ///     Асинхронно отправляет сокращенный URL в чат VkPlayLive.
    /// </summary>
    /// <param name="url">URL для сокращения.</param>
    /// <param name="context">Контекст чата, содержащий информацию о текущем канале и пользователе.</param>
    public Task SendShortenedUrlAsync(string url, ChatContext context)
    {
        return ExecuteIfConfigured(async () =>
        {
            if (context is not VkPlayChatContext vkPlayChatContext) return;
            // Сокращение URL перед отправкой
            var shortUrl = await urlShortenerService.ShrinkUrlAsync(url).ConfigureAwait(false);
            shortUrl ??= "Картинка не найдена.";
            await messageSender.PostMessageAsync(shortUrl, vkPlayChatContext.MessageId).ConfigureAwait(false);
        });
    }

    /// <summary>
    ///     Асинхронно отправляет сообщение в чат VkPlayLive с учетом цензуры текста.
    /// </summary>
    /// <param name="message">Текст сообщения для отправки.</param>
    public Task SendMessageToChannelAsync(string message)
    {
        return ExecuteIfConfigured(async () =>
        {
            // Применение цензуры к сообщению
            var censoredMessage = censorshipService.ApplyCensorship(message, MaxMessageLength, true);
            await messageSender.PostMessageAsync(censoredMessage).ConfigureAwait(false);
        });
    }

    /// <summary>
    ///     Создаёт и возвращает новый экземпляр VkPlayConnectionManager.
    /// </summary>
    /// <param name="configService">Сервис для получения конфигурационных данных.</param>
    /// <param name="webSocketManager">Менеджер WebSocket-соединений.</param>
    /// <param name="vkPlayAuthService">Сервис сессии VK Live.</param>
    /// <returns>Возвращает экземпляр <see cref="VkPlayConnectionManager" />.</returns>
    private static VkPlayConnectionManager CreateConnectionManager(ConfigService configService, WebSocketConnectionManager webSocketManager,
        VkPlayAuthService vkPlayAuthService)
    {
        return new VkPlayConnectionManager(webSocketManager, configService, vkPlayAuthService);
    }

    /// <summary>
    ///     Создаёт и возвращает новый экземпляр VkPlayMessageReceiver.
    /// </summary>
    /// <param name="webSocketManager">Менеджер WebSocket-соединений.</param>
    /// <param name="connectionManager">Менеджер подключения к VkPlayLive.</param>
    /// <returns>Возвращает экземпляр <see cref="VkPlayMessageReceiver" />.</returns>
    private static VkPlayMessageReceiver CreateMessageReceiver(WebSocketConnectionManager webSocketManager, VkPlayConnectionManager connectionManager)
    {
        return new VkPlayMessageReceiver(webSocketManager, connectionManager);
    }

    /// <summary>
    ///     Выполняет указанное действие, если сервис VkPlayChatService инициализирован и не завершён.
    ///     Это гарантирует, что действия не будут выполняться, если сервис не инициализирован или завершён.
    /// </summary>
    /// <param name="action">Функция, которая должна быть выполнена.</param>
    /// <param name="methodName">Имя метода, вызвавшего выполнение этой функции. Используется для логирования.</param>
    /// <returns>Задача, представляющая выполнение действия.</returns>
    private async Task ExecuteIfConfigured(Func<Task> action, [CallerMemberName] string methodName = "")
    {
        if (_isConfigured && !_isDisposed)
            try
            {
                await action().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Ошибка в методе {methodName}.");
                if (_connectionManager != null)
                    await _connectionManager.ReconnectAsync().ConfigureAwait(false);
            }
    }

    /// <summary>
    ///     Асинхронно обрабатывает входящее сообщение из VkPlayLive и выполняет соответствующие действия.
    /// </summary>
    /// <param name="message">Входящее сообщение в формате строки.</param>
    private async Task ProcessReceivedMessageAsync(string message)
    {
        try
        {
            // Проверка на валидность и обработка полученного сообщения
            if (messageProcessor.TryParseValidMessage(message, this, out var messageText, out var context))
            {
                await streamChatterStatsService.RecordMessageAsync(context.Username).ConfigureAwait(false);
                _ = chatAchievementService.TryRecordAsync(context.Username, messageText, "VkPlayLive");
                // Обработка сообщения с учетом упоминаний и цензуры
                var processedMessage = await messageHandler.HandleMessageAsync(messageText, context).ConfigureAwait(false);
                MessageReceived?.Invoke(this, new MessageReceivedEventArgs(processedMessage, context));
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при  обработке полученного сообщения на VkPlayLive.");
        }
    }
}