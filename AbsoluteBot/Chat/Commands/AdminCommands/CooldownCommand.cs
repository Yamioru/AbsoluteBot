using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services.CommandManagementServices;

namespace AbsoluteBot.Chat.Commands.AdminCommands;

/// <summary>
///     Команда добавления перезарядки ответов бота на команды для текущей платформы.
/// </summary>
public class CooldownCommand(CooldownService cooldownService) : BaseCommand, IParameterized
{
    public override int Priority => 700;
    public override string Description => "выставляет перезарядку команд бота на текущей платформе.";
    public override string Name => "!перезарядка";
    public string Parameters => "секунды";

    public override bool CanExecute(ParsedCommand command)
    {
        // Использовать могут модераторы и администраторы
        return command.UserRole is UserRole.Administrator or UserRole.Moderator;
    }

    protected override async Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        var seconds = command.Parameters;
        return await cooldownService.SetCooldownAsync(command.Context.Platform, seconds)
            .ConfigureAwait(false)
            ? $"Перезарядка установлена на {seconds} сек. для {command.Context.Platform}."
            : "Не получилось установить перезарядку.";
    }
}