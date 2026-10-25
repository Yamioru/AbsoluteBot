using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services.UtilityServices;

namespace AbsoluteBot.Chat.Commands.AdminCommands;

/// <summary>
///     Команда вывода всех пар ключ значение из конфига.
/// </summary>
public class ShowAllConfigCommand(ConfigService configService) : BaseCommand
{
    public override int Priority => 705;
    public override string Description => "выводит все пары ключ значение из конфига.";
    public override string Name => "!showconfig";

    public override bool CanExecute(ParsedCommand command)
    {
        // Использовать можно только в административных каналах администраторами
        return CommandPermissionChecker.IsAdministrativeChannel(command) && command.UserRole == UserRole.Administrator;
    }

    protected override Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        var configValues = configService.GetAllConfigValues();

        if (configValues.Count == 0) return Task.FromResult("Конфигурация пуста.");

        var result = string.Join("\n", configValues.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
        return Task.FromResult(result);
    }
}