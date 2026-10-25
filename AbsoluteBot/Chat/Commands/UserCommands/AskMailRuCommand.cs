using AbsoluteBot.Chat.Context;
using AbsoluteBot.Services;

namespace AbsoluteBot.Chat.Commands.UserCommands;

/// <summary>
///     Команда получения ответа на вопрос с помощью ответов MailRu.
/// </summary>
public class AskMailRuCommand(MailRuAnswerService mailRuAnswerService) : BaseCommand, IParameterized
{
    public override int Priority => 4;
    public override string Description => "выдаёт ответ на любой житейский вопрос из ответов mail.ru.";
    public override string Name => "!вопрос";
    public string Parameters => "вопрос";

    protected override async Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        return await mailRuAnswerService.AskAsync(command.Parameters).ConfigureAwait(false) ?? "Не удалось найти ответ.";
    }
}