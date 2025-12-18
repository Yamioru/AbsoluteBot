using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services.GoogleSheetsServices;
using AbsoluteBot.Services.UtilityServices;

namespace AbsoluteBot.Chat.Commands.UserCommands;

/// <summary>
///     Команда для добавления своей оценки для последней игры на стриме.
/// </summary>
public class GameRatingCommand(RateGoogleSheetsService rateGoogleSheetsService, ConfigService configService) : BaseCommand, IParameterized
{
    public override int Priority => 203;
    public override string Description => "поставить оценку текущей игре на стриме, оценка будет отображаться как оценка от чата.";
    public override string Name => "!оценка";
    public string Parameters => "цифра оценки от 0 до 10";

    public override bool CanExecute(ParsedCommand command)
    {
        // Могут использовать не игнорируемые пользователи в официально подключенных чатах всех сервисов
        return CommandPermissionChecker.IsOfficialChannel(command);
    }

    protected override async Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        var gameName = await configService.GetConfigValueAsync<string>("LastGameName").ConfigureAwait(false);
        var username = command.Context.Username;
        if (string.IsNullOrEmpty(gameName))
            return "Не удалось найти игру для оценки.";
        return await rateGoogleSheetsService.UpdateGameRatingAsync(command.Parameters, username, gameName).ConfigureAwait(false);
    }
}