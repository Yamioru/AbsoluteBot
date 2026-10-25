using AbsoluteBot.Chat.Context;
using AbsoluteBot.Services.MediaServices;

namespace AbsoluteBot.Chat.Commands.UserCommands;

/// <summary>
///     Команда получения картинки по запросу.
/// </summary>
public class ImageCommand(ImageSearchService imageSearchService) : BaseMediaCommand, IParameterized
{
    public override int Priority => 5;
    public override string Name => "!картинка";
    public override string Description => "выдаёт картинку по тексту запроса.";
    public string Parameters => "текст запроса";

    protected override async Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        var imageUrl = await imageSearchService.SearchImageAsync(command.Parameters).ConfigureAwait(false);
        await SendMediaResponseAsync(imageUrl, command).ConfigureAwait(false);
        return imageUrl;
    }
}