using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services;
using AbsoluteBot.Services.ChatServices.TwitchChat;
using AbsoluteBot.Services.NeuralNetworkServices;
using Serilog;

namespace AbsoluteBot.Chat.Commands.UserCommands;

/// <summary>
/// Команда для создания клипа на Twitch. Клип создаётся без названия длинной в 30 секунд.
/// </summary>
public class ClipCommand(TwitchChatService twitchChatService, INeuralAskService neuralAsk, ClipsService clipsService) : BaseCommand, IParameterized
{
    public override int Priority => 10;
    public override string Description => "делает клип со стрима на twitch или ищет уже сделанный по описанию.";
    public override string Name => "!клип";
    public string Parameters => "поисковый запрос или пусто";

    public override bool CanExecute(ParsedCommand command)
    {
        // Могут использовать не игнорируемые пользователи в официально подключенных чатах всех сервисов
        return command.UserRole != UserRole.Ignored && CommandPermissionChecker.IsOfficialChannel(command);
    }

    protected override async Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Parameters))
            return await twitchChatService.ClipCreate().ConfigureAwait(false)
                ? "Клип создан."
                : "Не получилось создать клип.";

        var clips = clipsService.GetAllClips().ToList();
        if (!clips.Any()) return "Список клипов пуст.";

        var clipDescriptions = string.Join(Environment.NewLine, clips.Select(c => $"Клип {c.Id}: \"{c.Name}\" - {c.Description}"));
        var gptRequest =
            $"Вот список клипов:{clipDescriptions}\nИскомый запрос: {command.Parameters}\nКакой номер клипа наиболее соответствует запросу? В ответе укажи только цифру номера клипа.";

        // Логирование запроса
        Log.Information("Gemini поиск клипа от {Username} на платформе {Platform}: запрос={Query}", 
            command.Context.Username, 
            command.Context.Platform, 
            command.Parameters);

        // Отправка запроса в Gemini
        var gptResponse = await neuralAsk.AskAsync(gptRequest, command.Context.MaxMessageLength).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(gptResponse))
        {
            Log.Warning("Gemini не смог найти клип для пользователя {Username} по запросу: {Query}", 
                command.Context.Username, 
                command.Parameters);
            return "Не удалось найти клип.";
        }

        Log.Information("Gemini успешно обработал поиск клипа для пользователя {Username}", command.Context.Username);

        // Попытка найти номер клипа в ответе
        var match = System.Text.RegularExpressions.Regex.Match(gptResponse, @"\d+");
        if (!match.Success) return "Не удалось найти клип..";

        var clipId = int.Parse(match.Value);
        var clip = clips.FirstOrDefault(c => c.Id == clipId);
        return clip != null ? clip.Url : "Не удалось найти клип...";
    }

    protected override bool HasRequiredParameters(ref ParsedCommand command)
    {
        return true;
    }
}