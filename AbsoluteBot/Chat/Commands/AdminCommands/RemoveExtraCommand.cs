using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services.CommandManagementServices;

namespace AbsoluteBot.Chat.Commands.AdminCommands;

/// <summary>
///     Команда удаления динамической команды из списка.
/// </summary>
public class RemoveExtraCommand(ExtraCommandsService extraCommandsService) : BaseCommand, IParameterized
{
    public override int Priority => 500;
    public override string Name => "!удалитькоманду";
    public override string Description => "удаляет дополнительную команду.";
    public string Parameters => "команда";

    public override bool CanExecute(ParsedCommand command)
    {
        // Использовать могут модераторы и администраторы
        return command.UserRole is UserRole.Administrator or UserRole.Moderator;
    }

    protected override async Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        return await extraCommandsService.RemoveCommandAsync(command.Parameters)
            .ConfigureAwait(false)
            ? $"Команда {command.Parameters} удалена."
            : $"Команда {command.Parameters} не найдена.";
    }
}