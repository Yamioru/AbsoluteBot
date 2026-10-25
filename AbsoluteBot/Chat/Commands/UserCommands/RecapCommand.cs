using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services.Recap;

namespace AbsoluteBot.Chat.Commands.UserCommands;

/// <summary>
/// Пересказ стрима или премиум-чата за выбранный срок.
/// </summary>
public class RecapCommand(ChatRecapService recapService) : BaseCommand
{
    public override int Priority => 181;
    public override string Description => "пересказывает, что было на стриме или в этом чате.";
    public override string Name => "!чтобыло";

    public override bool CanExecute(ParsedCommand command)
    {
        return command.UserRole != UserRole.Ignored && CommandPermissionChecker.IsOfficialChannel(command);
    }

    protected override async Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        var place = command.Context switch
        {
            TelegramChatContext => RecapPlace.Telegram,
            DiscordChatContext => RecapPlace.Discord,
            _ => RecapPlace.StreamChat
        };
        var parsed = RecapQueryParser.Parse(command.Parameters, place, RecapClock.Now);
        if (!parsed.Ok)
            return parsed.Error ?? RecapQueryParser.Hint;

        if (parsed.Source == RecapSource.Telegram && command.Context is not TelegramChatContext {ChannelType: ChannelType.Premium})
            return "Что было в Telegram, можно спросить только в премиум-канале Telegram.";
        if (parsed.Source == RecapSource.Discord &&
            command.Context is not DiscordChatContext {GuildType: ChannelType.Premium, ChatType: ChannelType.Premium})
            return "Что было в Discord, можно спросить только в премиум-канале Discord.";

        var userId = string.IsNullOrWhiteSpace(command.Context.UserId) ? command.Context.Username : command.Context.UserId;
        return await recapService.AnswerAsync(parsed, userId, command.Context.MaxMessageLength).ConfigureAwait(false);
    }
}