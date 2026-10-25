using AbsoluteBot.Chat.Commands.Registry;
using AbsoluteBot.Chat.Commands.UserCommands;
using AbsoluteBot.Chat.Context;
using AbsoluteBot.Services.CommandManagementServices;

namespace AbsoluteBot.Services.TextChat;

public class TextCommandService(
    ICommandRegistry commandRegistry,
    CommandExecutionService commandExecutionService)
{
    private const int DefaultMaxMessageLength = 512;
    private readonly List<(string Keyword, string CommandName)> _commandMatches = new();
    private bool _isInitialized;
    private string? _mentionCommandName;

    public async Task<string> ProcessAsync(string message, string username)
    {
        EnsureInitialized();

        message ??= string.Empty;
        var trimmedMessage = message.Trim();

        var contextChatService = new InMemoryChatService();
        var context = new TextChatContext(
            username,
            DefaultMaxMessageLength,
            contextChatService,
            null,
            null);

        var textForCommandExecution = TransformMessageForExecution(trimmedMessage);

        var result = await commandExecutionService.ExecuteCommandAsync(textForCommandExecution, context)
            .ConfigureAwait(false);

        // Некоторые команды возвращают “квиток” вместо текста, который они реально отправили.
        // Поэтому предпочитаем последний отправленный текст.
        var lastSent = contextChatService.LastSentMessage;
        if (!string.IsNullOrWhiteSpace(lastSent)) return lastSent;

        return result ?? "Я не знаю, что ответить";
    }

    private void EnsureInitialized()
    {
        if (_isInitialized) return;

        _mentionCommandName = commandRegistry.FindCommandByType<MentionCommand>()?.Name;

        foreach (var command in commandRegistry.GetAllCommands())
        {
            // В Telegram-подобной логике нам интересны только !-команды.
            if (!command.Name.StartsWith('!')) continue;

            var keyword = NormalizeCommandKeyword(command.Name);
            if (string.IsNullOrEmpty(keyword)) continue;

            _commandMatches.Add((Keyword: keyword, CommandName: command.Name));
        }

        _isInitialized = true;
    }

    private static string NormalizeCommandKeyword(string commandName)
    {
        // '!мудрость' => 'мудрость'
        // '!!загугли' => 'загугли'
        var token = commandName.Trim().ToLowerInvariant();
        token = token.TrimStart('!', '@');
        token = NormalizeWord(token);
        return token;
    }

    private static string NormalizeWord(string word)
    {
        var token = word.Trim().ToLowerInvariant();
        token = token.TrimStart('!', '@');
        token = token.TrimEnd('.', ',', '!', '?', ';', ':', ')', '(', '[', ']', '{', '}');
        return token;
    }

    private string TransformMessageForExecution(string trimmedMessage)
    {
        EnsureInitialized();

        if (string.IsNullOrEmpty(trimmedMessage)) return _mentionCommandName ?? "!мудрость";

        if (!string.IsNullOrEmpty(_mentionCommandName) &&
            trimmedMessage.StartsWith(_mentionCommandName, StringComparison.InvariantCultureIgnoreCase))
            return trimmedMessage;

        // Команды ищем по начальным словам (без ! / @), как в требовании.
        // Поддерживаем вариант "! мудрость" (с пробелом после '!'), как и в Telegram.
        var parts = trimmedMessage.Split((char[]?) null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return _mentionCommandName ?? "!мудрость";

        var tokenIndex = 0;
        var normalizedToken = NormalizeWord(parts[tokenIndex]);
        if (string.IsNullOrEmpty(normalizedToken) && parts.Length > 1)
        {
            tokenIndex = 1;
            normalizedToken = NormalizeWord(parts[tokenIndex]);
        }

        var remainderParts = parts.Skip(tokenIndex + 1);
        var remainder = string.Join(' ', remainderParts);

        var matched = _commandMatches
            .OrderByDescending(m => m.Keyword.Length)
            .FirstOrDefault(m =>
                string.Equals(m.Keyword, normalizedToken, StringComparison.InvariantCultureIgnoreCase));

        if (!string.IsNullOrEmpty(matched.CommandName))
            return string.IsNullOrEmpty(remainder) ? matched.CommandName : $"{matched.CommandName} {remainder}";

        // Если команда не распознана — выполняем MentionCommand.
        return string.IsNullOrEmpty(_mentionCommandName)
            ? trimmedMessage
            : $"{_mentionCommandName} {trimmedMessage}";
    }
}