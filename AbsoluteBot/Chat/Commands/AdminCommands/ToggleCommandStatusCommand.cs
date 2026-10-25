using AbsoluteBot.Chat.Context;
using AbsoluteBot.Services.CommandManagementServices;

namespace AbsoluteBot.Chat.Commands.AdminCommands;

/// <summary>
///     Команда переключения статуса включения команды на выбранной платформе.
/// </summary>
public class ToggleCommandStatusCommand(CommandStatusService commandStatusService) : BaseCommand, IParameterized
{
    public override int Priority => 504;
    public override string Description => "переключает статус работы команды на данной платформе (включает или выключает).";
    public override string Name => "!статускоманды";
    public string Parameters => "команда и платформа";

    public override bool CanExecute(ParsedCommand command)
    {
        // Использовать можно только в административных каналах
        return CommandPermissionChecker.IsAdministrativeChannel(command);
    }

    protected override async Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        var parameters = command.Parameters.Split(' ');
        if (parameters.Length < 2) return $"Использование: {Name} {Parameters}";

        var commandName = parameters[0];
        var serviceName = parameters[1];

        var currentStatus = await commandStatusService.IsCommandEnabled(commandName, serviceName).ConfigureAwait(false);
        if (!await commandStatusService.SetCommandStatusAsync(commandName, serviceName, !currentStatus).ConfigureAwait(false))
            return "Не удалось переключить состояние работы команды.";

        var statusText = !currentStatus ? "включена" : "отключена";
        return $"Команда {commandName} теперь {statusText} для сервиса {serviceName}.";
    }
}