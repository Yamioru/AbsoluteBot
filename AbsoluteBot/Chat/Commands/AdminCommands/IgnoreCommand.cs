using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services.UserManagementServices;

namespace AbsoluteBot.Chat.Commands.AdminCommands;

/// <summary>
///     Команда добавления пользователя в список игнорируемых ботом.
/// </summary>
public class IgnoreCommand(RoleService roleService) : BaseCommand, IParameterized
{
    public override int Priority => 700;
    public override string Description => "добавляет пользователь в игнор лист бота.";
    public override string Name => "!игнор";
    public string Parameters => "никнейм";

    public override bool CanExecute(ParsedCommand command)
    {
        // Использовать могут модераторы, администраторы и бот.
        return command.UserRole is UserRole.Administrator or UserRole.Moderator or UserRole.Bot;
    }

    protected override async Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        var username = command.Parameters;
        var currentRole = await roleService.GetUserRoleAsync(username).ConfigureAwait(false);
        if (currentRole == UserRole.Ignored)
        {
            await roleService.SetUserRoleAsync(username, UserRole.Default).ConfigureAwait(false);
            return $"{username} больше не игнорируется.";
        }

        await roleService.SetUserRoleAsync(username, UserRole.Ignored).ConfigureAwait(false);
        return $"{username} добавлен в список игнорируемых.";
    }
}