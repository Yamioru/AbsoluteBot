using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services.UserManagementServices;

namespace AbsoluteBot.Chat.Commands.UserCommands;

/// <summary>
///     Команда удаления оповещения о дне рождения на текущий платформе.
/// </summary>
public class RemoveBirthdayCommand(BirthdayService birthdayService) : BaseCommand
{
    public override int Priority => 305;
    public override string Description => "удаляет оповещение о вашем дне рождения на текущей платформе.";
    public override string Name => "!удалитьдр";

    public override bool CanExecute(ParsedCommand command)
    {
        // Могут использовать не игнорируемые пользователи в официально подключенных чатах всех сервисов
        return command.UserRole != UserRole.Ignored && CommandPermissionChecker.IsOfficialChannel(command);
    }

    protected override async Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        return await birthdayService.DisableBirthdayNotificationForPlatformAsync(command.Context.Username, command.Context.Platform,
            command.Context.UserId)
            .ConfigureAwait(false)
            ? $"Уведомления о дне рождения на платформе {command.Context.Platform} отключены."
            : "Не удалось отключить уведомления о вашем дне рождения.";
    }
}