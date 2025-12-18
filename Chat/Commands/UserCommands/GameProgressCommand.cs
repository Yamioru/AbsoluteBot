using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services;

namespace AbsoluteBot.Chat.Commands.UserCommands;

/// <summary>
///     Команда для получения прогресса прохождения последней игры на стриме.
/// </summary>
public class GameProgressCommand(GameProgressService gameProgressService) : BaseCommand
{
    public override int Priority => 200;
    public override string Description => "выдаёт процент прохождения текущей или последней игры на стриме.";
    public override string Name => "!прогресс";

    public override bool CanExecute(ParsedCommand command)
    {
        // Могут использовать не игнорируемые пользователи в официально подключенных чатах всех сервисов
        return CommandPermissionChecker.IsOfficialChannel(command);
    }

    protected override async Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        return await gameProgressService.GenerateProgressMessageAsync().ConfigureAwait(false);
    }
}