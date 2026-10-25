using System.Runtime.CompilerServices;
using AbsoluteBot.Chat.Context;
using AbsoluteBot.Events;
using AbsoluteBot.Services.ChatServices.Interfaces;
using AbsoluteBot.Services.UtilityServices;
using Discord;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using Serilog;

namespace AbsoluteBot.Services.ChatServices.Discord;

/// <summary>
///     Класс для обработки взаимодействия с Discord через API, предоставляющий функции отправки сообщений,
///     удаления сообщений, а также обработки событий, связанных с получением сообщений.
/// </summary>
public class DiscordChatService(ConfigService configService, DiscordGuildChannelService guildChannelService, DiscordMessageHandler messageHandler,
        DiscordMessageDataProcessor messageDataProcessor, DiscordImageProcessor imageProcessor)
    : IChatService, IMessagePreparationService, ISupportsMessageDeletion, IMarkdownMessageService, IAsyncInitializable, IAsyncDisposable, IChatImageService
{
    private const int ReconnectDelayMilliseconds = 5000;
    public const int MaxMessageLength = 2000;
    private bool _isConfigured;
    private bool _isDisposed;
    private DiscordNotificationService? _notificationService;
    private DiscordSocketClient? _client;
    private string? _discordToken;

    /// <summary>
    ///     Метод для освобождения ресурсов и завершения работы с Discord-сервисом.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        if (_client != null)
        {
            // Отписка от событий
            _client.MessageReceived -= HandleMessageReceivedAsync;
            _client.Disconnected -= OnClientDisconnected;

            // Остановка клиента Discord
            await _client.StopAsync().ConfigureAwait(false);
            await _client.DisposeAsync().ConfigureAwait(false);
            _client = null;
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Инициализирует необходимые сервисы для работы Discord.
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            _discordToken = await configService.GetConfigValueAsync<string>("DiscordToken").ConfigureAwait(false);
            if (string.IsNullOrEmpty(_discordToken))
            {
                Log.Warning("Не удалось инициализировать DiscordChatService, токен не найден.");
                return;
            }
            _isConfigured = true; // Конфигурация завершена
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при инициализации DiscordChatService");
            _isConfigured = false; // Если что-то пошло не так, сервис помечается как неконфигурированный
        }
    }

    /// <summary>
    ///     Асинхронно извлекает изображение в формате Base64 из сообщения Discord, если оно существует.
    /// </summary>
    /// <param name="context">Контекст чата Discord.</param>
    /// <param name="message">Полученное сообщение в чате.</param>
    /// <returns>Строка в формате Base64, представляющая изображение, или <c>null</c>, если изображение отсутствует.</returns>
    public async Task<string?> GetImageAsBase64Async(string message, ChatContext context)
    {
        if (context is not DiscordChatContext discordContext) return null;

        return await imageProcessor.GetImageAsBase64Async(discordContext);
    }

    /// <summary>
    ///     Событие, которое вызывается при получении нового сообщения в чате.
    /// </summary>
    public event EventHandler<MessageReceivedEventArgs>? MessageReceived;

    /// <summary>
    ///     Подключает клиента к Discord и инициирует обработку сообщений.
    /// </summary>
    public Task Connect()
    {
        return ExecuteIfServiceIsReady(async () =>
        {
            var config = new DiscordSocketConfig
            {
                AlwaysDownloadUsers = true,
                GatewayIntents = GatewayIntents.All
            };
            _client = new DiscordSocketClient(config);
            var messageSenderService = new DiscordMessageSenderService(_client);
            _notificationService = new DiscordNotificationService(guildChannelService, messageSenderService, _client);
            _client.MessageReceived += HandleMessageReceivedAsync;
            _client.Disconnected += OnClientDisconnected;
            await _client.LoginAsync(TokenType.Bot, _discordToken).ConfigureAwait(false);
            await _client.StartAsync().ConfigureAwait(false);
            Log.ForContext("ConnectionEvent", true).Information("Подключение к Discord произошло успешно.");
        });
    }

    /// <summary>
    ///     Асинхронно отправляет сообщение в чат Discord.
    /// </summary>
    /// <param name="message">Текст сообщения для отправки.</param>
    /// <param name="context">Контекст чата, содержащий данные для отправки сообщения.</param>
    /// <returns>Задача, представляющая выполнение операции отправки.</returns>
    public Task SendMessageAsync(string message, ChatContext context)
    {
        return ExecuteIfServiceIsReady(async () => { await DiscordMessageSenderService.SendMessageAsync(message, context).ConfigureAwait(false); });
    }

    /// <summary>
    ///     Асинхронно отправляет сообщение в чат Discord с Markdown.
    /// </summary>
    /// <param name="message">Текст сообщения для отправки.</param>
    /// <param name="context">Контекст чата, содержащий данные для отправки сообщения.</param>
    /// <returns>Задача, представляющая выполнение операции отправки.</returns>
    public Task SendMarkdownMessageAsync(string message, ChatContext context)
    {
        return ExecuteIfServiceIsReady(async () => { await DiscordMessageSenderService.SendMessageAsync(message, context).ConfigureAwait(false); });
    }

    /// <summary>
    ///     Готовит сообщение перед отправкой, вызывая эффект "печати" в чате Discord.
    /// </summary>
    /// <param name="context">Контекст чата, к которому относится сообщение.</param>
    /// <returns>Задача, представляющая выполнение операции подготовки сообщения.</returns>
    public Task PrepareMessageAsync(ChatContext context)
    {
        return ExecuteIfServiceIsReady(() =>
        {
            if (context is DiscordChatContext discordChatContext) discordChatContext.UserMessage?.Channel.TriggerTypingAsync();
            return Task.CompletedTask;
        });
    }

    /// <summary>
    ///     Асинхронно удаляет последние сообщения в чате Discord.
    /// </summary>
    /// <param name="context">Контекст чата, из которого необходимо удалить сообщения.</param>
    /// <param name="count">Количество сообщений для удаления.</param>
    /// <returns>Задача, представляющая выполнение операции удаления сообщений.</returns>
    public Task DeleteMessagesAsync(ChatContext context, int count)
    {
        return ExecuteIfServiceIsReady(async () =>
        {
            if (context is DiscordChatContext discordContext && _client?.GetChannel(discordContext.ChannelId) is IMessageChannel channel)
            {
                var messages = await channel.GetMessagesAsync(count).FlattenAsync().ConfigureAwait(false);
                foreach (var message in messages.Skip(1))
                    if (message is IUserMessage userMessage)
                        await userMessage.DeleteAsync().ConfigureAwait(false);
            }
        });
    }

    /// <summary>
    ///     Делает объявление в Discord чате оповещений.
    /// </summary>
    /// <param name="message">Сообщение для объявления.</param>
    public async Task AnnounceMessage(string message)
    {
        await ExecuteIfServiceIsReady(async () =>
        {
            if (_notificationService != null)
                await _notificationService.AnnounceMessageAsync(message).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    /// <summary>
    ///     Призывает пользователя в Discord с упоминанием в сообщении.
    /// </summary>
    /// <param name="username">Имя пользователя, который призывает.</param>
    /// <param name="usernameToCall">Имя пользователя, которого нужно призвать.</param>
    /// <returns>Задача с результатом успешного или неудачного выполнения призыва.</returns>
    public Task<string> SummonUser(string username, string usernameToCall)
    {
        return ExecuteIfServiceIsReadyWithResult(async () =>
        {
            if (_notificationService == null) return "Нет подключения к Discord.";
            return await _notificationService.SummonUser(username, usernameToCall).ConfigureAwait(false);
        }, "Нет подключения к Discord.");
    }

    /// <summary>
    ///     Выполняет указанное действие, если сервис Discord инициализирован и не завершён.
    ///     Это гарантирует, что действия не будут выполняться, если клиент не инициализирован или сервис завершён.
    /// </summary>
    /// <param name="action">Функция, которая должна быть выполнена.</param>
    /// <param name="methodName">Имя метода, вызвавшего выполнение этой функции. Используется для логирования.</param>
    /// <returns>Задача, представляющая выполнение действия.</returns>
    private async Task ExecuteIfServiceIsReady(Func<Task> action, [CallerMemberName] string methodName = "")
    {
        if (!_isConfigured || _isDisposed) return;

        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Ошибка в методе {methodName}");
        }
    }

    /// <summary>
    ///     Выполняет указанное действие, если сервис Discord инициализирован и не завершён, и возвращает результат действия.
    ///     Если сервис не инициализирован или завершён, возвращает значение по умолчанию.
    /// </summary>
    /// <typeparam name="T">Тип возвращаемого результата.</typeparam>
    /// <param name="action">Функция, которая должна быть выполнена.</param>
    /// <param name="defaultValue">Значение по умолчанию, возвращаемое в случае неудачи.</param>
    /// <param name="methodName">Имя метода, вызвавшего выполнение этой функции. Используется для логирования.</param>
    /// <returns>Задача, возвращающая результат выполнения функции или значение по умолчанию.</returns>
    private async Task<T> ExecuteIfServiceIsReadyWithResult<T>(Func<Task<T>> action, T defaultValue, [CallerMemberName] string methodName = "")
    {
        if (!_isConfigured || _isDisposed) return defaultValue;

        try
        {
            return await action().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Ошибка в методе {methodName}");
            return defaultValue;
        }
    }

    /// <summary>
    ///     Обрабатывает получение сообщения из Discord и инициирует его обработку.
    /// </summary>
    /// <param name="message">Сообщение, полученное в Discord.</param>
    private Task HandleMessageReceivedAsync(SocketMessage message)
    {
        return ExecuteIfServiceIsReady(async () =>
        {
            // Получение текста и контекста сообщения
            var parsedMessageResult = await messageDataProcessor.TryParseValidMessage(message, this).ConfigureAwait(false);
            if (parsedMessageResult == null) return;

            // Передача сообщения в обработчик сообщений
            var processedMessage = await messageHandler.HandleMessageAsync(parsedMessageResult.Value.text, parsedMessageResult.Value.context)
                .ConfigureAwait(false);

            // Вызов события для дальнейшей обработки
            MessageReceived?.Invoke(this, new MessageReceivedEventArgs(processedMessage, parsedMessageResult.Value.context));
        });
    }

    /// <summary>
    ///     Обрабатывает событие отключения клиента от сервера Discord.
    /// </summary>
    /// <param name="ex">Исключение, вызвавшее отключение.</param>
    /// <returns>Задача для выполнения асинхронной операции переподключения.</returns>
    private Task OnClientDisconnected(Exception ex)
    {
        return ExecuteIfServiceIsReady(async () =>
        {
            // Если ошибка не связана с переподключением по Gateway, ошибка логируется
            if (ex is not GatewayReconnectException)
                Log.ForContext("ConnectionEvent", true).Error(ex, "Соединение с Discord потеряно. Попытка переподключения...");
            // Отписка от событий

            if (_client != null)
            {
                _client.MessageReceived -= HandleMessageReceivedAsync;
                _client.Disconnected -= OnClientDisconnected;
                await _client.StopAsync().ConfigureAwait(false);
                await _client.DisposeAsync().ConfigureAwait(false);
                _client = null;
            }

            await Task.Delay(ReconnectDelayMilliseconds).ConfigureAwait(false);
            await Connect().ConfigureAwait(false);
        });
    }
}