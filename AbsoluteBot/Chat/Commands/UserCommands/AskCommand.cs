using AbsoluteBot.Chat.Context;
using AbsoluteBot.Services.NeuralNetworkServices;

namespace AbsoluteBot.Chat.Commands.UserCommands;

/// <summary>
///     Команда получения ответа на вопрос от Chat GPT.
/// </summary>
public class AskCommand(ChatGptService chatGptService) : BaseCommand, IParameterized
{
    public override int Priority => 1;
    public override string Description => "выдаёт ответ на практически любой вопрос с помощью нейросети.";
    public override string Name => "!спросить";
    public string Parameters => "вопрос";

    protected override async Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        return await chatGptService.AskChatGptAsync(command.Parameters, command.Context.MaxMessageLength).ConfigureAwait(false) ?? "Не удалось получить ответ.";
    }
}