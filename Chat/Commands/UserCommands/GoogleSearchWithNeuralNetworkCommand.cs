using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services.GoogleSearch;
using AbsoluteBot.Services.NeuralNetworkServices;
using AbsoluteBot.Services.UtilityServices;
using Serilog;

namespace AbsoluteBot.Chat.Commands.UserCommands;

/// <summary>
///     Получение нагугленной информации в интернете, сгруппированной с помощью нейросети.
/// </summary>
public class GoogleSearchWithNeuralNetworkCommand
    (IGoogleSearchService googleSearchService, AskGeminiService geminiService, WebContentService webContentService) : BaseCommand, IParameterized
{
    public override int Priority => 9;
    public override string Description => "выдаёт краткую сводку нагугленную информацию по тексту запроса.";
    public override string Name => "!!загугли";
    public string Parameters => "текст запроса";

    public override bool CanExecute(ParsedCommand command)
    {
        // Могут использовать не игнорируемые пользователи в официально подключенных чатах всех сервисов
        return command.UserRole != UserRole.Ignored && CommandPermissionChecker.IsOfficialChannel(command);
    }

    protected override async Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        // Логирование запроса
        Log.Information("Gemini Google поиск с группировкой от {Username} на платформе {Platform}: запрос={Query}", 
            command.Context.Username, 
            command.Context.Platform, 
            command.Parameters);

        // Получение url наглугленных страниц
        var googleUrlResults = await googleSearchService.PerformSearchAsync(command.Parameters).ConfigureAwait(false);
        if (googleUrlResults == null || googleUrlResults.Count == 0)
        {
            Log.Warning("Google поиск не дал результатов для пользователя {Username} по запросу: {Query}", 
                command.Context.Username, 
                command.Parameters);
            return "Ничего не нагуглилось.";
        }

        // Получение текстов с нагугленных страниц
        var resultsMainText = await webContentService.GetWebContentAsync(googleUrlResults).ConfigureAwait(false);
        if (resultsMainText == null)
        {
            Log.Warning("Не удалось получить содержимое веб-страниц для пользователя {Username} по запросу: {Query}", 
                command.Context.Username, 
                command.Parameters);
            return "Ничего не нагуглилось.";
        }

        //  Группировка полученного текста нейросетью
        var resultText =
            $"Нужно найти ответ на запрос: \"{command.Parameters}\", во что по этому поводу нашлось в интернете:\n".ToUpper() +
            string.Join('\n', resultsMainText);
        var geminiResponse = await geminiService.AskGeminiResponseAsync(resultText, command.Context.MaxMessageLength).ConfigureAwait(false);
        
        if (geminiResponse != null)
        {
            Log.Information("Gemini успешно сгруппировал результаты поиска для пользователя {Username}", command.Context.Username);
        }
        else
        {
            Log.Warning("Gemini не смог сгруппировать результаты поиска для пользователя {Username} по запросу: {Query}", 
                command.Context.Username, 
                command.Parameters);
        }
        
        return geminiResponse ?? "Ничего не нагуглилось.";
    }
}