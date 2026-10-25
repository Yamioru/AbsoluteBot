using System.Text;
using AbsoluteBot.Chat.Context;
using AbsoluteBot.Services.CommandManagementServices;

namespace AbsoluteBot.Chat.Commands.AdminCommands;

/// <summary>
///     Команда вывода статусов включенности команд на разных платформах.
/// </summary>
public class ShowCommandStatusesCommand(CommandStatusService commandStatusService) : BaseCommand
{
    public override int Priority => 502;
    public override string Description => "выводит список всех статусов команд на каждой из платформ (включены или нет).";
    public override string Name => "!статускоманд";

    public override bool CanExecute(ParsedCommand command)
    {
        // Использовать можно только в административных каналах
        return CommandPermissionChecker.IsAdministrativeChannel(command);
    }

    protected override Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        var statuses = commandStatusService.GetAllCommandStatuses();
        if (statuses.IsEmpty) return Task.FromResult("Нет зарегистрированных команд.");

        var groupedStatuses = statuses
            .GroupBy(entry => entry.Key.Item1) // Группировка по имени команды
            .ToDictionary(group => group.Key, group => group.Select(g => new {Platform = g.Key.Item2, Status = g.Value}));

        var result = new StringBuilder("Статус команд:\n");

        foreach (var commandEntry in groupedStatuses)
        {
            result.AppendLine($"\nКоманда: {commandEntry.Key}");
            foreach (var platformStatus in commandEntry.Value)
            {
                var statusText = platformStatus.Status ? "Включена" : "Отключена";
                result.AppendLine($"- Платформа: {platformStatus.Platform}, Статус: {statusText}");
            }
        }

        return Task.FromResult(result.ToString());
    }
}