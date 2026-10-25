using AbsoluteBot.Chat.Context;
using AbsoluteBot.Services.UtilityServices;

namespace AbsoluteBot.Chat.Commands.AdminCommands;

/// <summary>
///     Команда вывода списка всех цензурных слов.
/// </summary>
public class ShowCensorWordsCommand(CensorWordsService censorWordsService) : BaseCommand
{
    public override int Priority => 602;
    public override string Description => "выводит список всех цензурируемых слов.";
    public override string Name => "!цензура";

    public override bool CanExecute(ParsedCommand command)
    {
        // Использовать можно только в административных каналах
        return CommandPermissionChecker.IsAdministrativeChannel(command);
    }

    protected override Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        var censorWords = censorWordsService.GetAllCensorWords();
        return Task.FromResult(
            censorWords.IsEmpty
                ? "Список цензурируемых слов пуст."
                : string.Join(", ", censorWords));
    }
}