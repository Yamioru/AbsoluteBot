using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services.UtilityServices;

namespace AbsoluteBot.Chat.Commands.AdminCommands;

/// <summary>
///     Команда установки значения поля в конфиге.
/// </summary>
public class SetConfigValueCommand(ConfigService configService) : BaseCommand, IParameterized
{
    public override int Priority => 704;
    public override string Description => "изменяет значение поля в конфиге.";
    public override string Name => "!setconfig";
    public string Parameters => "ключ и значение";

    public override bool CanExecute(ParsedCommand command)
    {
        // Использовать можно только в административных каналах администраторами
        return CommandPermissionChecker.IsAdministrativeChannel(command) && command.UserRole == UserRole.Administrator;
    }

    protected override async Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        var parts = command.Parameters.Split(' ', 2);
        if (parts.Length < 2) return "Ошибка: необходимо указать ключ и значение, через пробел.";

        var key = parts[0];
        var value = parts[1];


        return await configService.SetConfigValueAsync(key, value)
            .ConfigureAwait(false)
            ? $"Значение для ключа '{key}' было установлено в '{value}'. Для вступления в силу, перезагрузите приложение."
            : "Не удалось сохранить значение в конфиге.";
    }
}