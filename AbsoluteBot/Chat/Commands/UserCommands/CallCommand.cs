using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services.ChatServices.Discord;

namespace AbsoluteBot.Chat.Commands.UserCommands;

/// <summary>
///     Команда для упоминания пользователя в Discord с целью позвать на стрим.
/// </summary>
public class CallCommand(DiscordChatService discordChatService) : BaseCommand, IParameterized
{
    public override int Priority => 201;
    public override string Description => "зовёт пользователя в дискорде по его никнейму там.";
    public override string Name => "!позвать";
    public string Parameters => "никнейм";

    public override bool CanExecute(ParsedCommand command)
    {
        // Может использовать не игнорируемый пользователь в чате стриминговых площадок
        return command.UserRole != UserRole.Ignored && CommandPermissionChecker.IsStreamingChannel(command);
    }

    protected override async Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        return await discordChatService.SummonUser(command.Context.Username, command.Parameters).ConfigureAwait(false);
    }
}