using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services;

namespace AbsoluteBot.Chat.Commands.UserCommands;

/// <summary>
///     Команда выдачи мудрости стримера.
/// </summary>
public class WisdomCommand(WisdomService wisdomService) : BaseCommand
{
    public override int Priority => 401;
    public override string Description => "выдаёт мудрость сказанную стримером (а может и не им).";
    public override string Name => "!мудрость";

    public override bool CanExecute(ParsedCommand command)
    {
        // Могут использовать не игнорируемые пользователи в официально подключенных чатах всех сервисов
        return CommandPermissionChecker.IsOfficialChannel(command);
    }

    protected override async Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        return await wisdomService.GetRandomWisdomAsync().ConfigureAwait(false) ?? "Не удалось получить мудрость.";
    }
}