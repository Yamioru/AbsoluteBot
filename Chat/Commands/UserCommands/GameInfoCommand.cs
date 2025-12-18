using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services.GoogleSheetsServices;
using AbsoluteBot.Services.UtilityServices;

namespace AbsoluteBot.Chat.Commands.UserCommands;

/// <summary>
///     Команда для получения информации о последней или выбранной игре на стриме.
/// </summary>
public class GameInfoCommand(GameGoogleSheetsService gameGoogleSheetsService, ConfigService configService) : BaseCommand, IParameterized
{
    public override int Priority => 202;
    public override string Description =>
        "выдаёт отзыв стримера, оценку и прочую информацию об игре, которая была когда-то на стриме или если игра не указана, то для текущей.";
    public override string Name => "!обигре";
    public string Parameters => "игра или пусто";

    public override bool CanExecute(ParsedCommand command)
    {
        // Могут использовать не игнорируемые пользователи в официально подключенных чатах всех сервисов
        return CommandPermissionChecker.IsOfficialChannel(command);
    }

    protected override async Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        var gameName = command.Parameters;
        if (string.IsNullOrEmpty(gameName))
            gameName = await configService.GetConfigValueAsync<string>("LastGameName").ConfigureAwait(false);
        if (string.IsNullOrEmpty(gameName)) return "Не удалось найти информацию об игре.";
        return await gameGoogleSheetsService.FetchGameInfoAsync(gameName).ConfigureAwait(false) ?? "Не удалось найти информацию об игре.";
    }

    protected override bool HasRequiredParameters(ref ParsedCommand command)
    {
        return true;
    }
}