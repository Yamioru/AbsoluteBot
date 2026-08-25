using System.Runtime.CompilerServices;
using AbsoluteBot.Chat.Context;
using AbsoluteBot.Events;
using AbsoluteBot.Helpers;
using AbsoluteBot.Services.ChatServices.Interfaces;
using AbsoluteBot.Services.UtilityServices;
using Serilog;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace AbsoluteBot.Services.ChatServices.TelegramChat;

/// <summary>
/// Сервис для работы с Telegram, реализующий интерфейсы для отправки сообщений, документов, фотографий и управления
/// сообщениями.
/// </summary>
public class TelegramChatService(ConfigService configService, TelegramMessageDataProcessor messageDataProcessor,
        TelegramMessageHandler messageHandler,
        TelegramImageProcessor imageProcessor, ChatAchievementService chatAchievementService)
    : IChatService, IMessagePreparationService, IDisposable, ISupportsMessageDeletion,
        IMarkdownMessageService, IDocumentSendingService, IPhotoSendingService, IStickerSendingService, IAsyncInitializable, IChatImageService
{
    private const int ReconnectDelayMilliseconds = 5000;
    public const int MaxMessageLength = 4096;
    private const int MessageTimestampOffsetHours = 3;
    private bool _isConfigured;
    private bool _isDisposed;
    private CancellationTokenSource? _cts;
    private DateTime _dateTelegramConnect;
    private string? _token;
    private TelegramBotClient? _botClient;

    /// <summary>
    /// Инициализирует TelegramChatService, загружая конфигурацию.
    /// </summary>
    /// <returns>Задача, представляющая выполнение инициализации.</returns>
    public async Task InitializeAsync()
    {
        try
        {
            _token = await configService.GetConfigValueAsync<string>("TelegramBotApiKey").ConfigureAwait(false);
            if (string.IsNullOrEmpty(_token))
            {
                Log.Warning("Не удалось инициализировать TelegramChatService, токен не найден.");
                return;
            }

            _botClient = new TelegramBotClient(_token);
            _isConfigured = true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при инициализации TelegramChatService");
            _isConfigured = false;
        }
    }

    /// <summary>
    /// Асинхронно извлекает изображение в формате Base64 из сообщения Telegram, если оно существует.
    /// </summary>
    /// <param name="context">Контекст чата Telegram.</param>
    /// <param name="message">Полученное сообщение в чате.</param>
    /// <returns>Строка в формате Base64, представляющая изображение, или <c>null</c>, если изображение отсутствует.</returns>
    public async Task<string?> GetImageAsBase64Async(string message, ChatContext context)
    {
        if (_botClient == null || _isDisposed || !_isConfigured || context is not TelegramChatContext telegramChatContext) return null;
        return await imageProcessor.GetBase64FileOrImageFromMessageAsync(_botClient, telegramChatContext).ConfigureAwait(false);
    }

    /// <summary>
    /// Событие, которое вызывается при получении нового сообщения в чате.
    /// </summary>
    public event EventHandler<MessageReceivedEventArgs>? MessageReceived;

    /// <summary>
    /// Подключает клиента к Telegram и начинает обработку сообщений.
    /// </summary>
    public Task Connect()
    {
        return ExecuteIfServiceIsReady(botClient =>
        {
            if (_token == null) return Task.CompletedTask;

            lock (this) // Использование блокировки для защиты доступа к _cts
            {
                // Сохраняем текущую дату подключения для проверки новых сообщений
                _dateTelegramConnect = DateTime.Now;

                // Проверка существования и состояния _cts перед отменой
                if (_cts != null)
                {
                    if (!_cts.IsCancellationRequested) _cts.Cancel();
                    _cts.Dispose();
                }

                // Создание нового CancellationTokenSource
                _cts = new CancellationTokenSource();
            }

            // Начало получения сообщений с использованием нового токена
            botClient.StartReceiving(HandleBotUpdate, HandleBotError, cancellationToken: _cts.Token);
            Log.ForContext("ConnectionEvent", true).Information("Соединение с Telegram установлено.");

            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// Отправляет сообщение в чат Telegram.
    /// </summary>
    /// <param name="message">Текст сообщения.</param>
    /// <param name="context">Контекст чата, содержащий данные для отправки сообщения.</param>
    /// <returns>Задача, представляющая выполнение операции отправки сообщения.</returns>
    public Task SendMessageAsync(string message, ChatContext context)
    {
        return ExecuteIfServiceIsReady(async botClient =>
        {
            if (context is not TelegramChatContext telegramContext) return;
            var replyParameters = new ReplyParameters
            {
                MessageId = telegramContext.MessageId
            };
            await SendTextCoreAsync(botClient, telegramContext.ChannelId, message, replyParameters).ConfigureAwait(false);
        });
    }

    /// <summary>
    /// Освобождает ресурсы и завершает работу с TelegramBotClient.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _botClient = null;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Отправляет документ в чат Telegram по указанному URL.
    /// </summary>
    /// <param name="url">URL документа для отправки.</param>
    /// <param name="context">Контекст чата, содержащий данные для отправки документа.</param>
    /// <returns>Задача, представляющая выполнение операции отправки документа.</returns>
    public Task SendDocumentAsync(string url, ChatContext context)
    {
        return ExecuteIfServiceIsReady(async botClient =>
        {
            if (context is not TelegramChatContext telegramContext) return;
            var file = new InputFileUrl(url);
            var replyParameters = new ReplyParameters
            {
                MessageId = telegramContext.MessageId
            };
            await botClient.SendDocument(telegramContext.ChannelId, file,
                replyParameters: replyParameters).ConfigureAwait(false);
        });
    }

    /// <summary>
    /// Отправляет сообщение с поддержкой Markdown разметки в чат Telegram.
    /// </summary>
    /// <param name="message">Текст сообщения с Markdown-разметкой.</param>
    /// <param name="context">Контекст чата, содержащий данные для отправки сообщения.</param>
    /// <returns>Задача, представляющая выполнение операции отправки сообщения.</returns>
    public Task SendMarkdownMessageAsync(string message, ChatContext context)
    {
        return SendMessageAsync(message, context);
    }

    /// <summary>
    /// Подготавливает сообщение перед отправкой, показывая эффект "печати" в Telegram.
    /// </summary>
    /// <param name="context">Контекст чата, в котором нужно показать действие печати.</param>
    /// <returns>Задача, представляющая выполнение операции подготовки сообщения.</returns>
    public Task PrepareMessageAsync(ChatContext context)
    {
        return ExecuteIfServiceIsReady(async botClient =>
        {
            if (context is not TelegramChatContext telegramContext) return;
            await botClient.SendChatAction(telegramContext.ChannelId, ChatAction.Typing).ConfigureAwait(false);
        });
    }

    /// <summary>
    /// Отправляет фотографию в чат Telegram по указанному URL.
    /// </summary>
    /// <param name="url">URL фотографии для отправки.</param>
    /// <param name="context">Контекст чата, содержащий данные для отправки фотографии.</param>
    /// <returns>Задача, представляющая выполнение операции отправки фотографии.</returns>
    public Task SendPhotoAsync(string url, ChatContext context)
    {
        return ExecuteIfServiceIsReady(async botClient =>
        {
            if (context is not TelegramChatContext telegramContext) return;
            var file = new InputFileUrl(url);
            var replyParameters = new ReplyParameters
            {
                MessageId = telegramContext.MessageId
            };
            await botClient.SendPhoto(telegramContext.ChannelId, file,
                replyParameters: replyParameters, hasSpoiler: telegramContext.isSpoilerMessage).ConfigureAwait(false);
        });
    }

    /// <summary>
    /// Отправляет фотографию в чат Telegram по указанному base64Image.
    /// </summary>
    /// <param name="base64Image">base64Image строка фотографии для отправки.</param>
    /// <param name="context">Контекст чата, содержащий данные для отправки фотографии.</param>
    /// <returns>Задача, представляющая выполнение операции отправки фотографии.</returns>
    public Task SendPhotoBase64Async(string base64Image, ChatContext context)
    {
        return ExecuteIfServiceIsReady(async botClient =>
        {
            if (context is not TelegramChatContext telegramContext) return;

            var imageBytes = Convert.FromBase64String(base64Image);
            using var stream = new MemoryStream(imageBytes);
            var file = new InputFileStream(stream);
            var replyParameters = new ReplyParameters
            {
                MessageId = telegramContext.MessageId
            };
            await botClient.SendPhoto(telegramContext.ChannelId, file,
                replyParameters: replyParameters).ConfigureAwait(false);
        });
    }

    /// <summary>
    /// Отправляет стикер в указанный канал Telegram.
    /// </summary>
    /// <param name="sticker">Идентификатор стикера.</param>
    /// <param name="context">Контекст чата, содержащий данные для отправки сообщения.</param>
    /// <returns>Задача, представляющая выполнение операции отправки сообщения.</returns>
    public Task SendStickerAsync(string sticker, ChatContext context)
    {
        return ExecuteIfServiceIsReady(async botClient =>
        {
            if (context is not TelegramChatContext telegramContext) return;
            var stickerInput = new InputFileId(sticker);
            await botClient.SendSticker(telegramContext.ChannelId, stickerInput).ConfigureAwait(false);
        });
    }

    /// <summary>
    /// Удаляет последние сообщения в чате Telegram.
    /// </summary>
    /// <param name="context">Контекст чата, содержащий данные о канале и сообщениях.</param>
    /// <param name="count">Количество сообщений для удаления.</param>
    /// <returns>Задача, представляющая выполнение операции удаления сообщений.</returns>
    public Task DeleteMessagesAsync(ChatContext context, int count)
    {
        return ExecuteIfServiceIsReady(async botClient =>
        {
            if (context is TelegramChatContext telegramContext)
                await TelegramMessageDeletionHandler.DeleteMessagesAsync(botClient, telegramContext.ChannelId, telegramContext.MessageId, count)
                    .ConfigureAwait(false);
        });
    }

    /// <summary>
    /// Отправляет сообщение в указанный канал Telegram.
    /// </summary>
    /// <param name="message">Текст сообщения для отправки.</param>
    /// <param name="channel">Идентификатор канала.</param>
    /// <returns>Задача, представляющая выполнение операции отправки сообщения.</returns>
    public Task SendMessageToChannelAsync(string message, string channel)
    {
        return ExecuteIfServiceIsReady(async botClient =>
        {
            var channelId = Convert.ToInt64(channel);
            await SendTextCoreAsync(botClient, channelId, message, null).ConfigureAwait(false);
        });
    }

    /// <summary>
    ///     Отправляет HTML в канал. Если Telegram отклонит разметку — уйдёт обычный текст без тегов.
    /// </summary>
    public Task SendHtmlMessageToChannelAsync(string html, string channel)
    {
        return ExecuteIfServiceIsReady(async botClient =>
        {
            var channelId = Convert.ToInt64(channel);
            try
            {
                await botClient.SendMessage(channelId, html, parseMode: ParseMode.Html).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "HTML-разметка Telegram отклонена, отправка без разметки.");
                await botClient.SendMessage(channelId, TextProcessingUtils.CleanHtmlTags(html)).ConfigureAwait(false);
            }
        });
    }

    /// <summary>
    /// Отправляет фотографию в указанный канал Telegram по URL.
    /// </summary>
    /// <param name="url">URL фотографии для отправки.</param>
    /// <param name="channel">Идентификатор канала.</param>
    /// <returns>Задача, представляющая выполнение операции отправки фотографии.</returns>
    public Task SendPhotoToChannelAsync(string url, string channel)
    {
        return ExecuteIfServiceIsReady(async botClient =>
        {
            var file = new InputFileUrl(url);
            var channelId = Convert.ToInt64(channel);
            await botClient.SendPhoto(channelId, file).ConfigureAwait(false);
        });
    }

    /// <summary>
    /// Отправляет сообщение в канал Telegram и закрепляет его как важное.
    /// </summary>
    /// <param name="message">Сообщение для отправки.</param>
    /// <param name="channel">Идентификатор канала.</param>
    /// <returns>Задача, представляющая выполнение операции отправки и закрепления сообщения.</returns>
    public Task SendPinnedMessageToChannelAsync(string message, string channel)
    {
        return ExecuteIfServiceIsReady(async botClient =>
        {
            var channelId = Convert.ToInt64(channel);
            var sentMessage = await SendTextCoreAsync(botClient, channelId, message, null).ConfigureAwait(false);
            await botClient.PinChatMessage(channelId, sentMessage.MessageId).ConfigureAwait(false);
        });
    }

    /// <summary>
    /// Открепляет последнее закрепленное сообщение в указанном канале Telegram.
    /// </summary>
    /// <param name="channel">Идентификатор канала.</param>
    /// <returns>Задача, представляющая выполнение операции открепления сообщения.</returns>
    public Task UnPinLastMessage(string channel)
    {
        return ExecuteIfServiceIsReady(async botClient =>
        {
            var channelId = Convert.ToInt64(channel);
            await botClient.UnpinChatMessage(channelId).ConfigureAwait(false);
        });
    }

    private static async Task<Message> SendTextCoreAsync(ITelegramBotClient botClient, long channelId, string message,
        ReplyParameters? replyParameters)
    {
        if (TelegramHtmlFormatter.TryFormat(message, MaxMessageLength, out var html))
        {
            try
            {
                return await botClient.SendMessage(channelId, html, replyParameters: replyParameters, parseMode: ParseMode.Html)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Разметка Telegram отклонена, отправка без разметки.");
            }
        }

        return await botClient.SendMessage(channelId, TextProcessingUtils.CutSentence(message, MaxMessageLength),
            replyParameters: replyParameters).ConfigureAwait(false);
    }

    /// <summary>
    /// Выполняет указанное действие с клиентом Telegram, если клиент не равен null, сервис инициализирован и не завершён.
    /// Это гарантирует, что действия не будут выполняться, если клиент не инициализирован или завершён.
    /// </summary>
    /// <param name="action">Функция, которая должна быть выполнена с клиентом Telegram.</param>
    /// <param name="methodName">Имя метода, вызвавшего выполнение этой функции. Используется для логирования.</param>
    /// <returns>Задача, представляющая выполнение действия с клиентом Telegram.</returns>
    private async Task ExecuteIfServiceIsReady(Func<ITelegramBotClient, Task> action, [CallerMemberName] string methodName = "")
    {
        if (_botClient != null && !_isDisposed && _isConfigured)
            try
            {
                await action(_botClient).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка в методе {MethodName} при выполнении операции с Telegram _botClient.",
                    methodName);
            }
    }

    /// <summary>
    /// Получает временную метку сообщения из обновления Telegram.
    /// </summary>
    /// <param name="update">Обновление Telegram.</param>
    /// <returns>Временная метка сообщения или null, если не найдено.</returns>
    private static DateTime? GetMessageTimestamp(Update update)
    {
        return update.Message?.Date ?? update.EditedMessage?.Date;
    }

    /// <summary>
    /// Обрабатывает ошибки, возникшие в боте Telegram, и инициирует переподключение при необходимости.
    /// </summary>
    /// <param name="client">Клиент Telegram, который вызвал ошибку.</param>
    /// <param name="exception">Исключение, вызвавшее ошибку.</param>
    /// <param name="token">Токен отмены операции.</param>
    /// <returns>Задача, представляющая выполнение обработки ошибки.</returns>
    private async Task HandleBotError(ITelegramBotClient client, Exception exception, CancellationToken token)
    {
        Log.ForContext("ConnectionEvent", true).Error(exception, "Произошла ошибка в Telegram боте.");

        if (exception is ApiRequestException apiException)
            // Если ошибка связана с конфликтом получения обновлений
            if (apiException.Message.Contains("terminated by other getUpdates request"))
            {
                Log.Error("Ошибка конфликта: возможно, запущено несколько экземпляров бота. Переподключение остановлено.");
                return;
            }

        if (_botClient != null)
        {
            // Отмена текущего цикла получения сообщений
            _cts?.Cancel();
            _cts?.Dispose();

            // Ожидание перед повторной попыткой переподключения
            await Task.Delay(ReconnectDelayMilliseconds).ConfigureAwait(false);

            // Переподключение: начало нового цикла получения сообщений
            await Connect().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Обрабатывает обновления, поступающие от Telegram, включая новые и отредактированные сообщения.
    /// </summary>
    /// <param name="client">Клиент Telegram, который вызвал обновление.</param>
    /// <param name="update">Обновление, содержащее информацию о новом или отредактированном сообщении.</param>
    /// <param name="token">Токен отмены операции.</param>
    /// <returns>Задача, представляющая выполнение обработки обновления.</returns>
    private async Task HandleBotUpdate(ITelegramBotClient client, Update update, CancellationToken token)
    {
        try
        {
            // Проверка времени сообщения для того, чтобы обрабатывать только новые сообщения
            if (!IsNewMessage(update))
                return;

            // Обработка текстовых и отредактированных сообщений
            var message = update.Message ?? update.EditedMessage;
            if (message == null) return;

            switch (message.Type)
            {
                case MessageType.Text:
                    await HandleMessageReceivedAsync(message).ConfigureAwait(false);
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при обработке обновления в Telegram.");
        }
    }

    /// <summary>
    /// Обрабатывает получение текстового сообщения из Telegram и инициирует его обработку.
    /// </summary>
    /// <param name="message">Объект сообщения из Telegram.</param>
    /// <returns>Задача, представляющая выполнение обработки сообщения.</returns>
    private async Task HandleMessageReceivedAsync(Message message)
    {
        // Получение текста и контекста сообщения
        var parsedMessageResult = await messageDataProcessor.TryParseValidMessage(message, this).ConfigureAwait(false);
        if (parsedMessageResult == null) return;

        // Передача сообщения в обработчик сообщений
        var parsed = parsedMessageResult.Value;
        _ = chatAchievementService.TryRecordAsync(parsed.context.Username, parsed.text, "Telegram");

        var processedMessage =
            await messageHandler.HandleMessageAsync(parsed.text, parsed.context, message.EditDate != null)
                .ConfigureAwait(false);

        MessageReceived?.Invoke(this, new MessageReceivedEventArgs(processedMessage, parsed.context));
    }

    /// <summary>
    /// Проверяет, было ли сообщение отправлено после подключения бота к Telegram.
    /// </summary>
    /// <param name="update">Обновление сообщения, полученное от Telegram.</param>
    /// <returns>True, если сообщение отправлено после подключения бота, иначе False.</returns>
    private bool IsNewMessage(Update update)
    {
        var timestamp = GetMessageTimestamp(update)?.AddHours(MessageTimestampOffsetHours);
        return timestamp > _dateTelegramConnect;
    }
}