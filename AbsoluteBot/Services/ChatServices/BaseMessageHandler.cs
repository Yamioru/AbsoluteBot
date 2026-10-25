using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services.UserManagementServices;
using AbsoluteBot.Services.UtilityServices;
using Serilog;

namespace AbsoluteBot.Services.ChatServices;

/// <summary>
/// Базовый класс для обработки сообщений, который предоставляет общие методы для работы с историей сообщений и
/// случайными упоминаниями бота.
/// </summary>
public abstract class BaseMessageHandler(ConfigService configService, RoleService roleService) : IAsyncInitializable
{
    protected const int MaxLastMessageCount = 10;
    private const double DefaultRandomMentionProbability = 0.005;
    protected readonly ConcurrentQueue<string> LastMessages = new();
    protected readonly Random Random = new();
    protected string? CommonBotName;

    public virtual async Task InitializeAsync()
    {
        CommonBotName = await configService.GetConfigValueAsync<string>("BotName").ConfigureAwait(false);
        if (string.IsNullOrEmpty(CommonBotName)) Log.Warning("Не удалось загрузить общее имя бота.");
    }

    public bool IsMessageInvalid(string? message)
    {
        if (message == null) return true;
        // Шаблон: имя пользователя (буквы латиницы, кириллицы, включая ё, цифры, нижнее подчеркивание), двоеточие и сообщение
        var pattern = @"^[A-Za-zА-Яа-яЁё0-9_]+: .+";
        if (message.StartsWith("[Twitch]")) return false;
        // Проверка совпадения строки с шаблоном
        return !Regex.IsMatch(message, pattern);
    }

    /// <summary>
    /// Добавляет упоминание бота в начале текста, если его там нет.
    /// Если текст начинается с имени бота без символа "@", он будет добавлен.
    /// </summary>
    /// <param name="text">Текст, в котором нужно добавить упоминание бота.</param>
    /// <returns>Текст с добавленным упоминанием бота, если его не было.</returns>
    protected string AddBotMention(string text)
    {
        if (CommonBotName == null || text.StartsWith('!')) return text;
        var botMention = "@" + CommonBotName;

        if (text.StartsWith(botMention, StringComparison.InvariantCultureIgnoreCase)) return text;

        return text.StartsWith(CommonBotName, StringComparison.InvariantCultureIgnoreCase)
            ? $"@{text}"
            : $"{botMention} {text}";
    }

    /// <summary>
    /// Обрабатывает упоминания бота в сообщениях и добавляет случайные упоминания в зависимости от условий.
    /// Вероятность случайного упоминания можно передать через параметр.
    /// </summary>
    /// <param name="text">Текст сообщения для обработки упоминаний.</param>
    /// <param name="context">Контекст чата, содержащий данные о текущем сеансе чата.</param>
    /// <param name="randomMentionProbability">Вероятность случайного упоминания бота (по умолчанию 0.001).</param>
    /// <returns>Возвращает итоговое сообщение или null, если пользователь игнорируется.</returns>
    protected async Task<string?> HandleMention(string text, ChatContext context, double randomMentionProbability = DefaultRandomMentionProbability)
    {
        // Получение роли пользователя
        var userRole = await roleService.GetUserRoleAsync(context.Username, context.Platform, context.UserId).ConfigureAwait(false);
        if (userRole == UserRole.Ignored) return null;
        if (IsBotMentioned(text, context) && IsMessageInvalid(context.Reply?.Message))
        {
            LastMessages.Clear();
            text = AddBotMention(text);
        }
        else if (ShouldRandomlyMentionBot(randomMentionProbability))
        {
            text = AddBotMention(text);
            context.LastMessages = new List<string>(LastMessages);
            LastMessages.Clear();
        }

        return text;
    }

    protected abstract bool IsBotMentioned(string text, ChatContext context);

    /// <summary>
    /// Сохраняет последнее сообщение пользователя в очередь последних сообщений.
    /// </summary>
    /// <param name="username">Имя пользователя, отправившего сообщение.</param>
    /// <param name="text">Текст сообщения.</param>
    protected void SaveLastMessage(string username, string text)
    {
        var formattedMessage = $"{username}: {text}";
        LastMessages.Enqueue(formattedMessage);
        while (LastMessages.Count > MaxLastMessageCount) LastMessages.TryDequeue(out _);
    }

    /// <summary>
    /// Проверяет, следует ли случайным образом упомянуть бота в сообщении.
    /// </summary>
    /// <param name="randomMentionProbability">Вероятность случайного упоминания.</param>
    /// <returns>True, если условия для случайного упоминания бота выполнены, иначе False.</returns>
    protected bool ShouldRandomlyMentionBot(double randomMentionProbability)
    {
        return LastMessages.Count > MaxLastMessageCount && Random.NextDouble() < randomMentionProbability;
    }
}