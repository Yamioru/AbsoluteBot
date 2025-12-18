using AbsoluteBot.Chat.Context;
using AbsoluteBot.Services.MediaServices;

namespace AbsoluteBot.Chat.Commands.UserCommands;

/// <summary>
/// Команда для получения нескольких изображений из Google.
/// </summary>
public class MultipleSpoilerImageCommand(ImageSearchService imageSearchService) : BaseMediaCommand, IParameterized
{
    public override int Priority => 6;
    public override string Name => "!спойлеркартинки";
    public override string Description => "выдаёт несколько картинок по тексту запроса и прячет их в спойлере.";
    public string Parameters => "текст запроса";

    public override bool CanExecute(ParsedCommand command)
    {
        // Использовать можно только в административных каналах или в премиумном телеграме
        return CommandPermissionChecker.IsAdministrativeChannel(command) ||
               command.Context is TelegramChatContext {ChannelType: ChannelType.Premium};
    }

    protected override async Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        const int length = 4;
        const int sendDelay = 500;
        var imageUrl = string.Empty;
        var imagesUrl = string.Empty;
        if (command.Context is TelegramChatContext tg)
            tg.isSpoilerMessage = true;
        // Выдача картинок циклом
        for (var i = 0; i < length; i++)
        {
            imagesUrl += imageUrl;
            imageUrl = await imageSearchService.SearchImageAsync(command.Parameters).ConfigureAwait(false);
            await SendMediaResponseAsync(imageUrl, command).ConfigureAwait(false);
            await Task.Delay(sendDelay).ConfigureAwait(false);
        }

        return imagesUrl;
    }
}