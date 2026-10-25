using AbsoluteBot.Chat.Context;
using AbsoluteBot.Services.UtilityServices;

namespace AbsoluteBot.Chat.Commands.AdminCommands;

/// <summary>
///     Команда для удаления цензурируемого слова из списка.
/// </summary>
public class RemoveCensorWordCommand(CensorWordsService censorWordsService) : BaseCommand, IParameterized
{
    public override int Priority => 601;
    public override string Description => "удаляет слово из списка цензурируемых слов.";
    public override string Name => "!удалитьцензуру";
    public string Parameters => "слово";

    public override bool CanExecute(ParsedCommand command)
    {
        // Использовать можно только в административных каналах
        return CommandPermissionChecker.IsAdministrativeChannel(command);
    }

    protected override async Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        return await censorWordsService.RemoveCensorWordAsync(command.Parameters)
            .ConfigureAwait(false)
            ? $"Слово '{command.Parameters}' было удалено из списка цензуры."
            : "Слово не было удалено из списка цензуры.";
    }
}