using System.Text;
using AbsoluteBot.Chat.Context;
using AbsoluteBot.Services;

namespace AbsoluteBot.Chat.Commands.AdminCommands;

/// <summary>
///     Команда вывода последних жалоб из списка.
/// </summary>
public class ShowComplaintsCommand(ComplaintService complaintService) : BaseCommand
{
    public override int Priority => 701;
    public override string Description => "выводит список всех жалоб.";
    public override string Name => "!жалобы";

    public override bool CanExecute(ParsedCommand command)
    {
        // Использовать можно только в административных каналах или в премиумном телеграме
        return CommandPermissionChecker.IsAdministrativeChannel(command) || command.Context is TelegramChatContext {ChannelType: ChannelType.Premium};
    }

    protected override Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        var complaints = complaintService.GetLastComplaints();
        if (complaints.Count == 0) return Task.FromResult("На данный момент жалоб нет.");

        var response = new StringBuilder("Последние жалобы:\n");
        for (var i = 0; i < complaints.Count; i++) response.AppendLine($"{i + 1}. {complaints[i]}");

        return Task.FromResult(response.ToString());
    }
}