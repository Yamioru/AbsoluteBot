using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services;

namespace AbsoluteBot.Chat.Commands.AdminCommands;

/// <summary>
///     Команда добавления новой мудрости в список мудростей.
/// </summary>
public class AddWisdomCommand(WisdomService wisdomService) : BaseCommand, IParameterized
{
    public override int Priority => 700;
    public override string Description => "добавить новую мудрость в список мудростей.";
    public override string Name => "!добавитьмудрость";
    public string Parameters => "текст мудрости";

    public override bool CanExecute(ParsedCommand command)
    {
        // Использовать могут модераторы и администраторы
        return command.UserRole is UserRole.Administrator or UserRole.Moderator;
    }

    protected override async Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        return await wisdomService.AddWisdomAsync(command.Parameters)
            .ConfigureAwait(false)
            ? "Мудрость добавлена!"
            : "Не получилось добавить мудрость.";
    }
}