using AbsoluteBot.Chat.Context;
using AbsoluteBot.Services.UtilityServices;

namespace AbsoluteBot.Chat.Commands.AdminCommands;

/// <summary>
///     Команда добавления нового слова в список цензурируемых слов.
/// </summary>
public class AddCensorWord(CensorWordsService censorWordsService) : BaseCommand, IParameterized
{
    public override int Priority => 600;
    public override string Description => "добавить цензурное слово для его замены в тексте.";
    public override string Name => "!добавитьцензуру";
    public string Parameters => "цензурное слово";

    public override bool CanExecute(ParsedCommand command)
    {
        // Использовать можно только в административных каналах
        return CommandPermissionChecker.IsAdministrativeChannel(command);
    }

    protected override async Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        var word = command.Parameters;
        return await censorWordsService.AddCensorWordAsync(word).ConfigureAwait(false)
            ? $"Слово '{word}' было добавлено в список для цензуры."
            : $"Не получилось добавить слово '{word}' в список для цензуры.";
    }
}