using AbsoluteBot.Chat.Context;
using AbsoluteBot.Helpers;
using AbsoluteBot.Services.NeuralNetworkServices;
using AbsoluteBot.Services.UserManagementServices;
using AbsoluteBot.Services.UtilityServices;
using Serilog;

namespace AbsoluteBot.Chat.Commands.UserCommands;

public class WhoSbtiCommand(SbtiService sbtiService, ConfigService configService, AskGeminiService askGeminiService,
    JsonSbtiCharacterCatalogService sbtiCatalogService) : BaseCommand, IParameterized
{
    public override int Priority => 302;
    public override string Description => "выдаёт какой ты (или выбранный пользователь) персонаж по системе SBTI из игры.";
    public override string Name => "!whosbti";
    public string Parameters => "игра или никнейм или пусто";

    public override bool CanExecute(ParsedCommand command)
    {
        return CommandPermissionChecker.IsOfficialChannel(command);
    }

    protected override async Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        var parameters = command.Parameters;
        var gameName = await configService.GetConfigValueAsync<string>("LastGameName").ConfigureAwait(false);
        var username = command.Context.Username;
        var userSbti = sbtiService.GetSbtiForUser(username);

        var targetGame = gameName;
        var targetUser = username;

        if (!string.IsNullOrWhiteSpace(parameters))
        {
            var userSbtiFromParam = sbtiService.GetSbtiForUser(parameters);
            if (userSbtiFromParam != null)
            {
                userSbti = userSbtiFromParam;
                targetUser = parameters;
            }
            else
            {
                targetGame = parameters;
            }
        }

        if (string.IsNullOrWhiteSpace(targetGame)) return "Не удалось найти игру.";

        if (string.IsNullOrWhiteSpace(userSbti))
            return "Ваш SBTI не найден. Установите его с помощью команды !sbti *КОД*. Пройти тест: https://sobekatypeb.github.io/SBTI/";

        // Проверяем локальный кэш
        var character = await sbtiCatalogService.TryGetCharacterAsync(targetGame, userSbti).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(character))
        {
            // Если в кэше нет — спрашиваем Gemini
            var excludeList = await sbtiCatalogService.GetCharactersForTitleAsync(targetGame).ConfigureAwait(false);
            var excludeJoined = excludeList.Count > 0 ? string.Join(", ", excludeList) : "—";

            // Достаем подробное описание типа, чтобы Gemini понимал суть мемного архетипа
            var sbtiDescription = SbtiService.GetSbtiDescription(userSbti);

            var message =
                $"Назови только имя 1 персонажа из тайтла \"{targetGame}\", который идеально подходит под архетип SBTI: {userSbti}. Характеристика архетипа: {sbtiDescription}. Обязательно загугли всех персонажей из данного тайтла и их краткое описание. ";

            var instruction = $"Ты эксперт по типированию персонажей. Выбери наиболее подходящего персонажа из вселенной \"{targetGame}\". " +
                              $"Если не знаешь — придумай из этой вселенной. " +
                              $"Верни ТОЛЬКО ИМЯ персонажа на английском языке (без пояснений, без кавычек, максимум 4 слова). " +
                              $"ОЧЕНЬ ВАЖНО: Запрещено использовать следующих персонажей: {excludeJoined}.";

            Log.Information("Gemini SBTI запрос: игра={Game}, SBTI={Sbti}, Исключены={Exclusions}", targetGame, userSbti, excludeJoined);

            var raw = await askGeminiService.AskGeminiResponseAsync(message, 64, instruction: instruction).ConfigureAwait(false);

            var candidate = TextProcessingUtils.SanitizeName(raw);
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                await sbtiCatalogService.SaveCharacterAsync(targetGame, userSbti, candidate).ConfigureAwait(false);
                character = candidate;
                Log.Information("Gemini SBTI успешно подобрал: {Character}", character);
            }
            else
            {
                Log.Warning("Gemini SBTI не смог подобрать персонажа.");
            }
        }

        if (string.IsNullOrWhiteSpace(character))
            return "Не удалось найти или подобрать персонажа.";

        return targetUser == username
            ? $"По SBTI вы соответствуете персонажу {character} из {targetGame}."
            : $"По SBTI {targetUser} соответствует персонажу {character} из {targetGame}.";
    }

    protected override bool HasRequiredParameters(ref ParsedCommand command)
    {
        return true;
    }
}