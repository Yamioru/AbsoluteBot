using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services.UtilityServices;

namespace AbsoluteBot.Chat.Commands.AdminCommands;

/// <summary>
///     Команда перезагрузки приложения.
/// </summary>
public class RestartCommand(IProcessController processController) : BaseCommand
{
    public override int Priority => -11;
    public override string Name => "!перезагрузка";
    public override string Description => "перезапускает бота (в Docker — перезапуск контейнера).";

    public override bool CanExecute(ParsedCommand command)
    {
        // Использовать могут администраторы
        return command.UserRole == UserRole.Administrator;
    }

    protected override Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        Task.Run(processController.Restart);
        return Task.FromResult("Перезагрузка приложения...");
    }
}