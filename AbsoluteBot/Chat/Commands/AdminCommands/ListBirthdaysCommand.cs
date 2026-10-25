using System.Text;
using AbsoluteBot.Chat.Context;
using AbsoluteBot.Services.UserManagementServices;

namespace AbsoluteBot.Chat.Commands.AdminCommands;

/// <summary>
///     Команда вывода списка всех дней рождений пользователей и то включены ли уведомления о них на каждой из платформ.
/// </summary>
public class ListBirthdaysCommand(BirthdayService birthdayService) : BaseCommand
{
    public override int Priority => 703;
    public override string Description => "выдаёт дни рождения пользователей и информацию о том включены ли упоминания дни рождения на каждой из платформ.";
    public override string Name => "!списокдр";

    public override bool CanExecute(ParsedCommand command)
    {
        // Использовать можно только в административных каналах
        return CommandPermissionChecker.IsAdministrativeChannel(command);
    }

    protected override Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        var birthdays = birthdayService.GetAllBirthdays();

        if (birthdays.Count == 0) return Task.FromResult("Дни рождения не найдены.");

        var result = new StringBuilder("Дни рождения пользователей:\n");
        foreach (var userBirthday in birthdays)
        {
            result.AppendLine($"{userBirthday.UserName} - {userBirthday.DateOfBirth:dd.MM}");
            foreach (var platform in userBirthday.NotifyOnPlatforms.Keys)
            {
                var status = userBirthday.NotifyOnPlatforms[platform] ? "Включены" : "Отключены";
                result.AppendLine($"  {platform}: {status}");
            }
        }

        return Task.FromResult(result.ToString());
    }
}