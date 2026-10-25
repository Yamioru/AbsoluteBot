using AbsoluteBot.Chat.Context;
using AbsoluteBot.Services;

namespace AbsoluteBot.Chat.Commands.UserCommands;

/// <summary>
///     Команда для отправки жалобы на работу бота.
/// </summary>
public class ComplaintCommand(ComplaintService complaintService) : BaseCommand, IParameterized
{
    public override int Priority => 405;
    public override string Description => "отправляет жалобу по поводу работы бота.";
    public override string Name => "!жалоба";
    public string Parameters => "текст жалобы";

    protected override async Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        return await complaintService.AddComplaintAsync(command.Parameters)
            .ConfigureAwait(false)
            ? "Ваша жалоба была успешно записана."
            : "Не получилось добавить жалобу:)";
    }
}