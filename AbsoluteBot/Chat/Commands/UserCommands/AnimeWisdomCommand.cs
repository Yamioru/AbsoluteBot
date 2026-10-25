using AbsoluteBot.Chat.Context;
using AbsoluteBot.Services.NeuralNetworkServices;

namespace AbsoluteBot.Chat.Commands.UserCommands;

/// <summary>
///     Команда для получение мудрости из случайного или выбранного аниме.
/// </summary>
public class AnimeWisdomCommand(INeuralAskService neuralAsk) : BaseCommand, IParameterized
{
    public override int Priority => 402;
    public override string Description => "выводит мудрость из аниме.";
    public override string Name => "!анимудрость";
    public string Parameters => "название аниме или пусто";

    protected override async Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        var anime = command.Parameters;

        var query = string.IsNullOrEmpty(anime)
            ? "Приведи мудрую цитату персонажа из случайного аниме, укажи имя персонажа в формате: \"Цитата\" – Имя персонажа;"
            : $"Приведи мудрую цитату персонажа из аниме {anime}, укажи имя персонажа в формате: \"Цитата\" – Имя персонажа;";

        return await neuralAsk.AskAsync(query, command.Context.MaxMessageLength).ConfigureAwait(false) ??
               "Не удалось получить анимудрость.";
    }

    protected override bool HasRequiredParameters(ref ParsedCommand command)
    {
        return true;
    }
}