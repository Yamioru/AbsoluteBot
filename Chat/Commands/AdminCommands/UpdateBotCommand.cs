using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services.UtilityServices;

namespace AbsoluteBot.Chat.Commands.AdminCommands;

/// <summary>
///     Команда обновления бота на VDS: git pull и пересборка Docker через host-watcher.
/// </summary>
public class UpdateBotCommand(IBotUpdateService updateService) : BaseCommand
{
    public override int Priority => -10;
    public override string Name => "!обновить";
    public override string Description => "обновляет бота на сервере (git pull + docker compose).";

    public override bool CanExecute(ParsedCommand command) =>
        command.UserRole == UserRole.Administrator;

    protected override async Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        await updateService.RequestUpdateAsync().ConfigureAwait(false);
        return "Обновление запрошено. Watcher на сервере сделает git pull и пересборку Docker.";
    }
}
