using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services.UserManagementServices;

namespace AbsoluteBot.Chat.Commands.UserCommands;

/// <summary>
///     Команда получения количества дней до дня рождения ближайшего или выбранного пользователя.
/// </summary>
public class BirthdayCommand(BirthdayService birthdayService) : BaseCommand, IParameterized
{
    public override int Priority => 303;
    public override string Description =>
        "выдаёт сколько дней осталось до дня рождения пользователя или сколько дней осталось до ближайшего дня рождения среди зарегистрированных пользователей.";
    public override string Name => "!др";
    public string Parameters => "никнейм или пусто";

    public override bool CanExecute(ParsedCommand command)
    {
        // Могут использовать не игнорируемые пользователи в официально подключенных чатах всех сервисов
        return CommandPermissionChecker.IsOfficialChannel(command);
    }

    protected override Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        var context = command.Context;
        var platform = context.Platform;

        // Если передан никнейм, выводится количество дней до дня рождения указанного пользователя
        if (command.Parameters.Length > 0)
        {
            var username = command.Parameters;
            return Task.FromResult(birthdayService.GetTimeUntilUserBirthdayForPlatform(username, platform));
        }

        // Если никнейм не передан, выводится количество дней до ближайшего дня рождения среди зарегистрированных пользователей
        return Task.FromResult(birthdayService.GetTimeUntilNextBirthdayForPlatform(platform));
    }

    protected override bool HasRequiredParameters(ref ParsedCommand command) => true;
}