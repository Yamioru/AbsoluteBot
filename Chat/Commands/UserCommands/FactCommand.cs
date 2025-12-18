using AbsoluteBot.Chat.Context;
using AbsoluteBot.Services;
using AbsoluteBot.Services.NeuralNetworkServices;

namespace AbsoluteBot.Chat.Commands.UserCommands;

/// <summary>
///     Команда для получения случайного факта из википедии и не только.
/// </summary>
public class FactCommand(FactService factService, AskGeminiService geminiService) : BaseCommand
{
    public override int Priority => 407;
    public override string Description => "выдаёт какой-то интересный или не очень факт из википедии и не только.";
    public override string Name => "!факт";

    protected override async Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        var fact = await factService.GetFactAsync().ConfigureAwait(false);
        if (string.IsNullOrEmpty(fact))
        {
            fact = await geminiService.AskGeminiResponseAsync("Расскажи интересный случайный факт на случайную тему.", 200,
                instruction: "Он должен быть небольшим, не больше пары предложений", temperature: 2.0);
        }
        return fact ?? "Не удалось раздобыть факт.";
    }
}