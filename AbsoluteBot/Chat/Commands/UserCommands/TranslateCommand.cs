using System.Text.RegularExpressions;
using AbsoluteBot.Chat.Context;
using AbsoluteBot.Services;

namespace AbsoluteBot.Chat.Commands.UserCommands;

/// <summary>
///     Команда для получения перевода текст на русский, или с русского на английский.
/// </summary>
public partial class TranslateCommand(TranslationService translationService) : BaseCommand, IParameterized
{
    public override int Priority => 10;
    public override string Description => "переводит данный текст на русский язык, или если он и так на русском, то на английский.";
    public override string Name => "!переведи";
    public string Parameters => "текст";

    protected override async Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        var targetText = string.IsNullOrEmpty(command.Parameters) ? command.Context.Reply!.Message : command.Parameters;
        var targetLanguage = CyrillicRegex().IsMatch(targetText) ? "EN" : "RU";

        return await translationService.TranslateTextAsync(targetText, targetLanguage).ConfigureAwait(false) ??
               "Не могу понять, тут что-то на эльфийском.";
    }

    /// <summary>
    ///     Метод проверки наличия параметров команды, если они необходимы.
    /// </summary>
    /// <param name="command">Команда</param>
    /// <returns>True если параметры не нужны или присутствуют. False если необходимые параметры не найдены</returns>
    protected override bool HasRequiredParameters(ref ParsedCommand command)
    {
        if (this is not IParameterized parameterizedCommand || !string.IsNullOrEmpty(command.Parameters) || !string.IsNullOrEmpty(command.Context.Reply?.Message)) return true;
        command.Response = $"Использование: {Name} *{parameterizedCommand.Parameters}*";
        command.Context.ChatService.SendMessageAsync(command.Response, command.Context);
        return false;
    }

    [GeneratedRegex(@"\p{IsCyrillic}")]
    private static partial Regex CyrillicRegex();
}