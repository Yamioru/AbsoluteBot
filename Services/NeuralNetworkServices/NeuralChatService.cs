using System.Text.RegularExpressions;
using AbsoluteBot.Helpers;
using AbsoluteBot.Models;
using AbsoluteBot.Services;
using AbsoluteBot.Services.UtilityServices;
using Serilog;

namespace AbsoluteBot.Services.NeuralNetworkServices;
#pragma warning disable IDE0028
/// <summary>
///     Надстройка диалогового чата: история, платформы, постобработка. Генерацию отдаёт Gemini или Groq.
/// </summary>
public partial class NeuralChatService(
    ConfigService configService,
    NeuralModelConfigService modelConfig,
    GroqChatService groqService,
    GeminiChatClient geminiChat) : INeuralChatService, IAsyncInitializable
{
    private const string ModelGeminiName = "model";
    private const string UserGeminiName = "user";
    private const int MaxOutputTokens = 2000;
    private const double InitialTemperature = 0.8;
    private const int MaxGenerationAttempts = 3;
    private readonly Dictionary<string, ChatHistory> _platformChatHistories = new();
    private readonly SemaphoreSlim _chatAsyncSemaphore = new(1, 1);
    private string? _botName;

    public async Task InitializeAsync()
    {
        _botName = await configService.GetConfigValueAsync<string>("BotName").ConfigureAwait(false);
        if (string.IsNullOrEmpty(_botName))
            Log.Warning("Не удалось загрузить общее имя бота для gemini.");
        await InitializePlatformChatHistories().ConfigureAwait(false);
    }
    private readonly Dictionary<string, DateTime> _lastMessageTimes = new(); // Словарь для хранения времени последнего сообщения каждой платформы.

    /// <summary>
    ///     Проверка времени последнего сообщения для указанной платформы.
    /// </summary>
    /// <param name="platform">Название платформы.</param>
    /// <returns>Задача для выполнения проверки.</returns>
    private async Task CheckLastMessageTimeAsync(string platform)
    {
        if (_lastMessageTimes.TryGetValue(platform, out var lastMessageTime))
        {
            var timeSinceLastMessage = DateTime.UtcNow - lastMessageTime;

            if (timeSinceLastMessage.TotalHours > 6)
            {
                var message = $"Прошло {Math.Floor(timeSinceLastMessage.TotalHours)} часов. Возможно прошлая тема беседы уже не актуальна и её не стоит вспоминать.";
                await _platformChatHistories[platform].AddMessageAsync(UserGeminiName, $"System: {message}", platform).ConfigureAwait(false);
            }
        }
        else
        {
            // Если платформа еще не зарегистрирована, добавляем текущую метку времени.
            _lastMessageTimes[platform] = DateTime.UtcNow;
        }
    }

    /// <summary>
    ///     Добавление сообщения пользователя в историю чата для всех платформ.
    /// </summary>
    /// <param name="message">Сообщение пользователя.</param>
    /// <param name="user">Имя пользователя.</param>
    /// <returns>True, если сообщение было успешно добавлено, иначе false.</returns>
    public async Task<bool> AddUserMessageToChatHistory(string message, string user)
    {
        try
        {
            foreach (var platformChat in _platformChatHistories)
                await platformChat.Value.AddMessageAsync(UserGeminiName, $"{user}: {message}", platformChat.Key).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при добавлении сообщения в история разговора с чат ботом.");
            return false;
        }
    }

    /// <summary>
    ///     Добавление сообщения пользователя в историю чата для выбранной платформы.
    /// </summary>
    /// <param name="message">Сообщение пользователя.</param>
    /// <param name="user">Имя пользователя.</param>
    /// <param name="platform">Платформа для общения (например, Discord, Telegram).</param>
    /// <returns>True, если сообщение было успешно добавлено, иначе false.</returns>
    public async Task<bool> AddUserMessageToChatHistoryOnPlatform(string message, string user, string platform)
    {
        try
        {
            await CheckLastMessageTimeAsync(platform);
            await _platformChatHistories[platform].AddMessageAsync(UserGeminiName, $"{user}: {message}", platform).ConfigureAwait(false);
            _lastMessageTimes[platform] = DateTime.UtcNow;
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при добавлении сообщения в история разговора с чат ботом.");
            return false;
        }
    }

    /// <summary>
    ///     Асинхронное общение с ботом для заданной платформы с учетом сообщения пользователя.
    /// </summary>
    /// <param name="userMessage">Сообщение пользователя.</param>
    /// <param name="replyInfo">Информация о сообщении на которое был дан ответ пользователем (если имеется).</param>
    /// <param name="platform">Платформа для общения (например, Discord, Telegram).</param>
    /// <param name="base64Image">
    ///     Строка, представляющая изображение в формате Base64. Если не указана, отправляется
    ///     только текст.
    /// </param>
    /// <returns>Ответ от модели или null в случае ошибки.</returns>
    public virtual async Task<string?> ChatAsync(string userMessage, ReplyInfo? replyInfo, string platform, string? base64Image = null)
    {
        return await ExecuteWithSemaphoreAsync(async () =>
        {
            // Получение истории чата для текущей платформы
            var chatHistory = await GetChatHistoryForPlatformAsync(platform).ConfigureAwait(false);

            // Уведомление о том, что прошло много времени с последней беседы
            await CheckLastMessageTimeAsync(platform);

            // Обработка сообщения на которое ответил пользователь
            if (replyInfo != null) await HandleReplyAsync(replyInfo, chatHistory, platform).ConfigureAwait(false);

            // Обработка и добавление сообщения в историю
            userMessage = PreProcessMessage(userMessage);

            // Добавляет сообщение с изображением (если оно есть) в историю
            await chatHistory.AddMessageAsync(UserGeminiName, $"{userMessage}", platform, base64Image).ConfigureAwait(false);

            // Генерация ответа
            return await GenerateResponseAsync(platform, chatHistory).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    /// <summary>
    ///     Асинхронное общение с ботом на основе нескольких сообщений для заданной платформы.
    /// </summary>
    /// <param name="userMessages">Коллекция сообщений пользователя.</param>
    /// <param name="platform">Платформа для общения (например, Discord, Telegram).</param>
    /// <returns>Ответ от модели или null в случае ошибки.</returns>
    public virtual async Task<string?> ChatAsync(IEnumerable<string> userMessages, string platform)
    {
        return await ExecuteWithSemaphoreAsync(async () =>
        {
            // Получение истории чата для текущей платформы
            var chatHistory = await GetChatHistoryForPlatformAsync(platform).ConfigureAwait(false);

            // Добавление специального сообщения
            await chatHistory.AddMessageAsync(UserGeminiName,
                "System: ПРОШЛО НЕКОТОРОЕ ВРЕМЯ, БЕСЕДА ВОЗМОЖНО ПЕРЕШЛА В ДРУГОЕ РУСЛО.", platform).ConfigureAwait(false);

            // Обработка и добавление сообщений в историю
            foreach (var userMessage in userMessages)
            {
                var preprocessedMessage = PreProcessMessage(userMessage);
                await chatHistory.AddMessageAsync(UserGeminiName, $"{preprocessedMessage}", platform).ConfigureAwait(false);
            }

            // Генерация ответа
            return await GenerateResponseAsync(platform, chatHistory).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    /// <summary>
    ///     Очистка истории чата для указанной платформы, кроме первых двух сообщений.
    /// </summary>
    /// <param name="platform">Название платформы (например, Discord, Telegram).</param>
    /// <returns>True, если история успешно очищена, иначе false.</returns>
    public virtual async Task<bool> ClearPlatformChatHistoryAsync(string platform)
    {
        try
        {
            var chatHistory = await GetChatHistoryForPlatformAsync(platform).ConfigureAwait(false);
            await chatHistory.ClearExceptFirstTwo().ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Ошибка при очистке памяти у чат бота для платформы {platform}.");
            return false;
        }
    }

    [GeneratedRegex("<<(.*?)>>")]
    private static partial Regex CommandExpressionRegex();

    /// <summary>
    ///     Выполняет указанную асинхронную задачу с использованием семафора для предотвращения одновременного выполнения.
    /// </summary>
    /// <typeparam name="T">Тип результата, который возвращает выполняемая задача.</typeparam>
    /// <param name="action">Функция, представляющая асинхронную задачу, которая будет выполнена.</param>
    /// <returns>Результат выполнения задачи или значение по умолчанию для типа T в случае ошибки.</returns>
    /// <remarks>
    ///     Метод гарантирует, что задача будет выполнена с блокировкой семафора для предотвращения одновременных вызовов.
    ///     Если возникает исключение, оно логируется, и возвращается значение по умолчанию.
    /// </remarks>
    private async Task<T?> ExecuteWithSemaphoreAsync<T>(Func<Task<T>> action)
    {
        await _chatAsyncSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_botName == null)
                return default;
            if (!string.Equals(modelConfig.GetProvider(), NeuralProviders.Groq, StringComparison.OrdinalIgnoreCase)
                && !geminiChat.HasApiKeys)
                return default;
            return await action().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при выполнении задачи с использованием семафора.");
            return default;
        }
        finally
        {
            _chatAsyncSemaphore.Release();
        }
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex ExtraSpacesRegex();

    /// <summary>
    /// Создание замен для слов в промпте
    /// </summary>
    /// <param name="platform">Платформа, для которой генерируется ответ.</param>
    /// <returns></returns>
    private async Task<Dictionary<string, string>> BuildReplacements(string platform)
    {
        var streamWasOnline = await configService.GetConfigValueAsync<bool>("StreamWasOnline").ConfigureAwait(false);
        var gameName = await configService.GetConfigValueAsync<string>("LastGameName").ConfigureAwait(false);
        var news = await configService.GetConfigValueAsync<string>("News").ConfigureAwait(false);
        return new Dictionary<string, string>
        {
            ["Date"] = DateTime.Now.ToString("yyyy-MM-dd"),
            ["StreamState"] = streamWasOnline ? "Включен" : "Выключен",
            ["LastGameName"] = gameName ?? "Не найдена",
            ["Platform"] = platform,
            ["News"] = news ?? "Новостей нет" 
        };
    }


    /// <summary>
    ///     Генерация ответа модели на основе текущей истории чата для платформы.
    /// </summary>
    /// <param name="temperature">Параметр температуры для генерации (влияет на креативность ответа).</param>
    /// <param name="platform">Платформа, для которой генерируется ответ.</param>
    /// <returns>Сгенерированный ответ или null, если возникла ошибка.</returns>
    private async Task<string?> GenerateModelResponseAsync(double temperature, string platform)
    {
        var chatHistory = await GetChatHistoryForPlatformAsync(platform).ConfigureAwait(false);
        var replacements = await BuildReplacements(platform).ConfigureAwait(false);
        var model = modelConfig.GetModel(NeuralEntities.Chat);

        if (string.Equals(modelConfig.GetProvider(), NeuralProviders.Groq, StringComparison.OrdinalIgnoreCase))
            return await groqService.CompleteAsync(chatHistory.ToGroqMessages(replacements), model, temperature)
                .ConfigureAwait(false);

        return await geminiChat.CompleteAsync(chatHistory, replacements, temperature, model, MaxOutputTokens)
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Генерирует ответ от модели Gemini для текущей платформы на основе истории чата.
    /// </summary>
    /// <param name="platform">Платформа, с которой пришло сообщение.</param>
    /// <param name="chatHistory">История чата для текущей платформы.</param>
    /// <returns>Ответ модели Gemini или null, если не удалось сгенерировать ответ.</returns>
    private async Task<string?> GenerateResponseAsync(string platform, ChatHistory chatHistory)
    {
        var temperature = InitialTemperature;

        // Попытка сгенерировать ответ с несколькими попытками, если модель возвращает повторяющийся результат
        for (var i = 0; i < MaxGenerationAttempts; i++)
        {
            var result = await GenerateModelResponseAsync(temperature, platform).ConfigureAwait(false);

            if (string.IsNullOrEmpty(result)) continue;

            // Постобработка сообщения (очистка от смайликов, лишних символов и прочего)
            result = PostProcessMessage(result);

            // Если результат дублируется, изменяется параметр температуры и повторяется
            var (isDuplicate, updatedTemperature) =
                await HandleDuplicateResponseAsync(chatHistory, result, platform, i, temperature).ConfigureAwait(false);
            temperature = updatedTemperature; // Обновление температуры после вызова

            if (isDuplicate) continue;

            // Добавление сгенерированного ответа в историю чата
            var chatMessage = $"{_botName}: {result}";
            await chatHistory.AddMessageAsync(ModelGeminiName, chatMessage, platform).ConfigureAwait(false);

            return result;
        }

        return null;
    }

    /// <summary>
    ///     Получение истории чата для указанной платформы, если она уже существует, или загрузка начальных сообщений.
    /// </summary>
    /// <param name="platform">Название платформы (например, Discord, Telegram).</param>
    /// <returns>История чата для платформы.</returns>
    private async Task<ChatHistory> GetChatHistoryForPlatformAsync(string platform)
    {
        if (_platformChatHistories.TryGetValue(platform, out var value)) return value;
        var chatHistory = new ChatHistory();
        await chatHistory.LoadInitialMessagesFromFileAsync(DataPaths.Get("initialUserMessage.txt"),
            DataPaths.Get("initialModelMessage.txt"), platform).ConfigureAwait(false);
        value = chatHistory;
        _platformChatHistories[platform] = value;

        return value;
    }

    /// <summary>
    ///     Асинхронная обработка дублирующегося сообщения в истории чата и корректировка параметров генерации.
    /// </summary>
    /// <param name="chatHistory">История чата платформы.</param>
    /// <param name="result">Сгенерированный ответ модели.</param>
    /// <param name="platform">Название платформы.</param>
    /// <param name="attempt">Текущий номер попытки генерации ответа.</param>
    /// <param name="temperature">Текущая температура генерации, изменяемая при необходимости.</param>
    /// <returns>True, если было обнаружено дублирующееся сообщение, иначе false.</returns>
    private async Task<(bool isDuplicate, double updatedTemperature)> HandleDuplicateResponseAsync(ChatHistory chatHistory, string result,
        string platform, int attempt, double temperature)
    {
        var chatMessage = $"{_botName}: {result}";
        if (!chatHistory.IsMessageInRecentHistory(ModelGeminiName, chatMessage)) return (false, temperature);
        switch (attempt)
        {
            case 0:
                await chatHistory.AddMessageAsync(UserGeminiName,
                    "System: НЕ ПОВТОРЯЙ СВОИ СООБЩЕНИЯ, СТАРАЙСЯ ПИСАТЬ БОЛЕЕ РАЗНООБРАЗНО.", platform).ConfigureAwait(false);
                break;
            default:
                temperature += 0.5;
                break;
        }

        return (true, temperature);
    }

    /// <summary>
    ///     Асинхронная обработка информации о сообщении на которое дал ответ пользователь для добавления в историю чата.
    /// </summary>
    /// <param name="replyInfo">
    ///     Информация о о сообщении на которое дал ответ пользователь, включающая пользователя и
    ///     сообщение.
    /// </param>
    /// <param name="chatHistory">История чата, в которую добавляется это сообщение.</param>
    /// <param name="platform">Название платформы, на которой идет общение.</param>
    private async Task HandleReplyAsync(ReplyInfo replyInfo, ChatHistory chatHistory, string platform)
    {
        var username = replyInfo.Username;
        var message = replyInfo.Message;
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(message)) return;
        var userMessage = $"{username}: {message}";
        userMessage = PreProcessMessage(userMessage);
        if (username.Equals(_botName, StringComparison.InvariantCultureIgnoreCase))
            await chatHistory.AddOrUpdateModelMessageAsync(ModelGeminiName, userMessage, platform).ConfigureAwait(false);
        else
            await chatHistory.AddOrUpdateUserMessageAsync(UserGeminiName, userMessage, platform).ConfigureAwait(false);
    }

    /// <summary>
    ///     Инициализирует историю чатов для каждой платформы, загружая начальные сообщения из файлов.
    /// </summary>
    /// <remarks>
    ///     Метод создает объекты истории чатов для указанных платформ и загружает для каждой платформы
    ///     начальные сообщения из файлов с помощью асинхронного вызова.
    /// </remarks>
    private async Task InitializePlatformChatHistories()
    {
        var platforms = new[] {"Discord", "Twitch", "Telegram", "VkPlayLive"};
        foreach (var platform in platforms)
        {
            var chatHistory = new ChatHistory();
            await chatHistory.LoadInitialMessagesFromFileAsync(DataPaths.Get("initialUserMessage.txt"), DataPaths.Get("initialModelMessage.txt"), platform).ConfigureAwait(false);
            _platformChatHistories[platform] = chatHistory;
        }
    }

    /// <summary>
    ///     Постобработка сообщения: удаление повторяющихся частей, смайлов и лишних пробелов.
    /// </summary>
    /// <param name="message">Исходное сообщение для постобработки.</param>
    /// <returns>Постобработанное сообщение.</returns>
    private string PostProcessMessage(string message)
    {
        message = TryExtractCommandExpression(message);
        // Удаление повторяющегося никнейма бота из начала строки
        if (_botName != null)
        {
            var pattern = $@"^({Regex.Escape(_botName)}:\s*)+";
            message = Regex.Replace(message, pattern, "", RegexOptions.IgnoreCase).Trim();
        }
        // Удаление описания действий
        const string removeStars = @"\*.*?\*";
        message = Regex.Replace(message, removeStars, "");
        // Удаление всех смайликов
        message = TextProcessingUtils.RemoveEmojis(message);

        // Удаление лишних пробелов
        message = ExtraSpacesRegex().Replace(message, " ").Trim();
        return message;
    }

    /// <summary>
    ///     Предобработка сообщения: удаление ненужных символов и приведение первой буквы к верхнему регистру.
    /// </summary>
    /// <param name="message">Исходное сообщение для предобработки.</param>
    /// <returns>Предобработанное сообщение.</returns>
    private static string PreProcessMessage(string message)
    {
        // Приведение первой буквы к верхнему регистру
        if (!string.IsNullOrEmpty(message)) message = char.ToUpper(message[0]) + message[1..];

        // Удаление всех символов, кроме русских и английских букв, знаков препинания и пробелов
        message = RuEnSpaceSignsRegex().Replace(message, "").Trim();

        return message;
    }

    [GeneratedRegex(@"[^a-zA-Zа-яА-ЯёЁ0-9\s\p{P}]")]
    private static partial Regex RuEnSpaceSignsRegex();

    /// <summary>
    ///     Метод для извлечения первого выражения команды вида &lt;&lt;!команда текст&gt;&gt; из строки, если оно есть
    /// </summary>
    /// <param name="input">Исходный текст</param>
    /// <returns>Команда, если она есть, если нет изначальный текст</returns>
    private static string TryExtractCommandExpression(string input)
    {
        // Поиск первого совпадения
        var match = CommandExpressionRegex().Match(input);

        // Если совпадение найдено, возвращается найденное выражение, иначе возвращается исходная строка
        return match.Success ? match.Groups[1].Value : input;
    }
}