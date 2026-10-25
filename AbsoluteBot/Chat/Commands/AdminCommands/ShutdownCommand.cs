using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services.UtilityServices;

namespace AbsoluteBot.Chat.Commands.AdminCommands;

/// <summary>
///     Команда выключения приложения.
/// </summary>
public class ShutdownCommand(IProcessController processController) : BaseCommand
{
    public override int Priority => -12;
    public override string Name => "!выключить";
    public override string Description => "выключает приложение.";

    public override bool CanExecute(ParsedCommand command)
    {
        // Использовать могут администраторы
        return command.UserRole == UserRole.Administrator;
    }

    protected override Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        Task.Run(processController.Shutdown);
        return Task.FromResult("Приложение выключается...");
    }
}