using AbsoluteBot.Chat.Context;
using AbsoluteBot.Services.GoogleSearch;
using AbsoluteBot.Services.NeuralNetworkServices;

namespace AbsoluteBot.Chat.Commands.UserCommands;

/// <summary>
///     Команда для получения определения из Google.
/// </summary>
public class DefinitionCommand(IGoogleSearchDefinitionService googleSearchDefinitionService, AskGeminiService askGeminiService) : BaseCommand, IParameterized
{
    public override int Priority => 3;
    public override string Description => "выдаёт определение слова или краткую сводку по данному тексту из google или из других источников.";
    public override string Name => "!определение";
    public string Parameters => "текст";

    protected override async Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        return await googleSearchDefinitionService.GetDefinitionAsync(command.Parameters, command.Context.MaxMessageLength).ConfigureAwait(false) ??
               await askGeminiService.AskGeminiResponseAsync(command.Parameters, command.Context.MaxMessageLength).ConfigureAwait(false) ??
               "Не удалось найти определение.";
    }
}