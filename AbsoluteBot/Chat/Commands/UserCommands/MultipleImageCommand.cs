using AbsoluteBot.Chat.Context;
using AbsoluteBot.Services.MediaServices;

namespace AbsoluteBot.Chat.Commands.UserCommands;

/// <summary>
///     Команда для получения нескольких изображений из Google.
/// </summary>
public class MultipleImageCommand(ImageSearchService imageSearchService) : BaseMediaCommand, IParameterized
{
    public override int Priority => 6;
    public override string Name => "!картинки";
    public override string Description => "выдаёт несколько картинок по тексту запроса.";
    public string Parameters => "текст запроса";

    protected override async Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        const int length = 4;
        const int sendDelay = 500;
        var imageUrl = string.Empty;
        var imagesUrl = string.Empty;
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