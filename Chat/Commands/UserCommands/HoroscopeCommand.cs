using AbsoluteBot.Chat.Context;
using AbsoluteBot.Services.NeuralNetworkServices;

namespace AbsoluteBot.Chat.Commands.UserCommands;

/// <summary>
///     Команда получения гороскопа на определенное время.
/// </summary>
public class HoroscopeCommand(AskGeminiService askGeminiService) : BaseCommand
{
    protected readonly Random Random = new();
    public override int Priority => 400;
    public override string Description => "выдаёт гороскоп на ближайшее время.";
    public override string Name => "!гороскоп";

    protected override async Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        var when = new List<string> {"сегодня", "завтра", "на этой неделе", "в этом месяце", "в этом году"};
        var whom = new List<string> {"вы", "вам", "вас"};
        var what = new List<string> {"положительного", "отрицательного", "нейтрального"};
        var text = $"Составь мне гороскоп {what[Random.Next(what.Count)]} характера, начиная с фразы: Согласно гороскопу " +
                   when[Random.Next(when.Count)] + " " + whom[Random.Next(whom.Count)];
        return await askGeminiService.AskGeminiResponseAsync(text, command.Context.MaxMessageLength).ConfigureAwait(false) ??
               "Текущее положение планет мешает составлению гороскопа, попробуйте позже.";
    }
}