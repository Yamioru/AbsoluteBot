using AbsoluteBot.Chat.Context;
using AbsoluteBot.Services.MediaServices;

namespace AbsoluteBot.Chat.Commands.UserCommands;

/// <summary>
/// Команда получения картинки по запросу со спойлером.
/// </summary>
public class SpoilerImageCommand(ImageSearchService imageSearchService) : BaseMediaCommand, IParameterized
{
    public override int Priority => 5;
    public override string Name => "!спойлеркартинка";
    public override string Description => "выдаёт картинку по тексту запроса и прячет её в спойлере.";
    public string Parameters => "текст запроса";

    public override bool CanExecute(ParsedCommand command)
    {
        // Использовать можно только в административных каналах или в премиумном телеграме
        return CommandPermissionChecker.IsAdministrativeChannel(command) ||
               command.Context is TelegramChatContext {ChannelType: ChannelType.Premium};
    }

    protected override async Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        var imageUrl = await imageSearchService.SearchImageAsync(command.Parameters).ConfigureAwait(false);
        if (command.Context is TelegramChatContext tg)
            tg.isSpoilerMessage = true;
        await SendMediaResponseAsync(imageUrl, command).ConfigureAwait(false);
        return imageUrl;
    }
}