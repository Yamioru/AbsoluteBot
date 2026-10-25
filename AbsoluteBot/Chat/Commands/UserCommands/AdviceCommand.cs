using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services;

namespace AbsoluteBot.Chat.Commands.UserCommands;

/// <summary>
/// Совет по игре, которая сейчас на стриме. Текст вопроса необязателен.
/// </summary>
public class AdviceCommand(StreamAdviceService adviceService) : BaseCommand
{
    public override int Priority => 180;
    public override string Description => "даёт совет по текущей игре на стриме, можно дописать вопрос.";
    public override string Name => "!совет";

    public override bool CanExecute(ParsedCommand command)
    {
        return command.UserRole != UserRole.Ignored && CommandPermissionChecker.IsOfficialChannel(command);
    }

    protected override Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        var key = string.IsNullOrWhiteSpace(command.Context.UserId) ? command.Context.Username : command.Context.UserId;
        return adviceService.AdviseAsync(command.Context.Platform + ":" + key, command.Parameters, command.Context.MaxMessageLength);
    }
}