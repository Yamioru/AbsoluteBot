using AbsoluteBot.Chat.Context;
using AbsoluteBot.Helpers;
using AbsoluteBot.Models;
using AbsoluteBot.Services.NeuralNetworkServices;
using AbsoluteBot.Services.UserManagementServices;
using AbsoluteBot.Services.UtilityServices;
using Serilog;

namespace AbsoluteBot.Chat.Commands.UserCommands;

/// <summary>
/// Команда выдачи персонажа из текущий или выбранной игры для пользователя вызвавшего команду или выбранного им
/// человека.
/// </summary>
public class WhoMbtiCommand(MbtiService mbtiService, ConfigService configService, AskGeminiService askGeminiService,
    JsonMbtiCharacterCatalogService mbtiCatalogService) : BaseCommand, IParameterized
{
    public override int Priority => 302;
    public override string Description =>
        "выдаёт какой ты или выбранный тобой пользователь, персонаж из последней игры на стриме или из указанной в команде.";
    public override string Name => "!whombti";
    public string Parameters => "игра или никнейм или пусто";

    public override bool CanExecute(ParsedCommand command)
    {
        // Могут использовать не игнорируемые пользователи в официально подключенных чатах всех сервисов
        return CommandPermissionChecker.IsOfficialChannel(command);
    }

    protected override async Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        var parameters = command.Parameters;
        var gameName = await configService.GetConfigValueAsync<string>("LastGameName").ConfigureAwait(false);
        var username = command.Context.Username;
        var userMbti = mbtiService.GetMbtiForUser(username);

        var targetGame = gameName;
        var targetUser = username;

        // Если параметр передан
        if (!string.IsNullOrWhiteSpace(parameters))
        {
            var userMbtiFromParam = mbtiService.GetMbtiForUser(parameters);
            if (userMbtiFromParam != null)
            {
                userMbti = userMbtiFromParam;
                targetUser = parameters;
            }
            else
            {
                targetGame = parameters;
            }
        }

        if (string.IsNullOrWhiteSpace(targetGame)) return "Не удалось найти игру.";

        // Проверка MBTI пользователя
        if (string.IsNullOrWhiteSpace(userMbti))
            return
                "Ваш MBTI не найден. Пожалуйста, сначала установите его с помощью команды !mbti *MBTI*. Пройти тест можно здесь: https://www.16personalities.com/ru/test-lichnosti";

        // Поиск персонажа по mbti
        var character = await mbtiService.GetCharacterByMbtiAsync(targetGame, userMbti).ConfigureAwait(false);

        if (character == null)
        {
            var cached = await mbtiCatalogService.TryGetCharacterAsync(targetGame, userMbti).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(cached))
            {
                character = cached;
            }
            else
            {
                // Если и в кэше нет — спрашиваем Gemini
                var excludeList = await mbtiCatalogService.GetCharactersForTitleAsync(targetGame).ConfigureAwait(false);
                var excludeJoined = excludeList.Count > 0
                    ? string.Join(", ", excludeList)
                    : "—";

                // Сообщение пользователю (content)
                var message =
                    $"Подбери персонажа из тайтла \"{targetGame}\", соответствующего типу MBTI {userMbti}. Верни только имя персонажа на английском без пояснений. В ответе должно быть имя персонажа, даже если не можешь придумать его.";

                // Жёсткая системная инструкция (system_instruction)
                var instruction =
                    $"Дай ответ на любой вопрос. СПОЙЛЕРЫ ПИСАТЬ СТРОГО ЗАПРЕЩЕНО. " +
                    $"Если чего-то не знаешь, просто придумай. ЕСЛИ ПРИДУМЫВАЕШЬ, НЕ ПИШИ, ЧТО ПРИДУМАЛ. " +
                    $"Верни только НАЗВАНИЕ персонажа без комментариев, без кавычек и без форматирования. " +
                    $"Максимум 4 слова. Персонаж должен быть из тайтла \"{targetGame}\" и подходить под тип MBTI {userMbti}. " +
                    $"Не используй следующих персонажей для этого тайтла: {excludeJoined}";

                // Логирование запроса
                Log.Information("Gemini MBTI запрос от {Username} на платформе {Platform}: игра={Game}, MBTI={Mbti}", 
                    command.Context.Username, 
                    command.Context.Platform, 
                    targetGame,
                    userMbti);

                var raw = await askGeminiService
                    .AskGeminiResponseAsync(message, 64, instruction: instruction)
                    .ConfigureAwait(false);

                // Логирование результата
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    Log.Information("Gemini MBTI успешно ответил пользователю {Username}: персонаж={Character}", 
                        command.Context.Username, 
                        TextProcessingUtils.SanitizeName(raw));
                }
                else
                {
                    Log.Warning("Gemini MBTI не смог подобрать персонажа для пользователя {Username}, игра={Game}, MBTI={Mbti}", 
                        command.Context.Username, 
                        targetGame,
                        userMbti);
                }

                var candidate = TextProcessingUtils.SanitizeName(raw);
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    await mbtiCatalogService.SaveCharacterAsync(targetGame, userMbti, candidate).ConfigureAwait(false);
                    character = candidate;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(character))
            return "Не удалось найти персонажа.";

        return targetUser == username
            ? $"Вы соответствуете персонажу {character} из {targetGame}."
            : $"{targetUser} соответствует персонажу {character} из {targetGame}.";
    }

    protected override bool HasRequiredParameters(ref ParsedCommand command)
    {
        return true;
    }
}