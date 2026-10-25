using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services.CommandManagementServices;

namespace AbsoluteBot.Chat.Commands.AdminCommands;

/// <summary>
///     Команда добавления дополнительной команды, которая будет выдавать указанный текст.
/// </summary>
public class AddExtraCommand(ExtraCommandsService extraCommandsService) : BaseCommand, IParameterized
{
    public override int Priority => 501;
    public override string Description => "добавить команду, которая отвечает заданным ответом.";
    public override string Name => "!добавитькоманду";
    public string Parameters => "команда и ответ";

    public override bool CanExecute(ParsedCommand command)
    {
        // Использовать могут модераторы и администраторы
        return command.UserRole is UserRole.Administrator or UserRole.Moderator;
    }

    protected override async Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        var parameters = command.Parameters.Split(' ', 2);

        if (parameters.Length < 2) return $"Неверный формат. Используйте: {Name} {Parameters}";

        var newCommand = parameters[0];
        var response = parameters[1];

        return await extraCommandsService.AddOrUpdateCommandAsync(newCommand, response)
            .ConfigureAwait(false)
            ? $"Команда {newCommand} добавлена/обновлена."
            : $"Команду {newCommand} добавить/обновить не получилось.";
    }
}