using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services.CommandManagementServices;

namespace AbsoluteBot.Chat.Commands;

/// <summary>
///     Класс дополнительных команд, которые задаются динамически модераторами и могут быть использованы для получения
///     заданного текста.
/// </summary>
public class ExecuteExtraCommand(ExtraCommandsService extraCommandsService) : IChatCommand
{
    public string Name => "Динамические команды";
    public int Priority => 1200;
    public string Description => "добавляются динамически из чата модераторами бота, вот их список:";

    public bool CanExecute(ParsedCommand command)
    {
        // Может быть использована если:
        return command.UserRole != UserRole.Ignored // не игнорируемые пользователи
               && CommandPermissionChecker.IsOfficialChannel(command) // в официально подключенных чатах всех сервисов
               && (extraCommandsService.GetCommand(command.Command) != null); // если такая команда существует
    }

    public async Task<string> ExecuteAsync(ParsedCommand command)
    {
        var response = extraCommandsService.GetCommand(command.Command);
        command.Response = string.IsNullOrEmpty(response) ? "Команда не найдена" : response;
        await command.Context.ChatService.SendMessageAsync(command.Response, command.Context).ConfigureAwait(false);
        return command.Response;
    }
}