using AbsoluteBot.Chat.Context;
using AbsoluteBot.Services.MediaServices;

namespace AbsoluteBot.Chat.Commands.UserCommands;

/// <summary>
///     Команда для получения гифки по запросу.
/// </summary>
public class GifCommand(GifSearchService gifSearchService) : BaseMediaCommand, IParameterized
{
    public override int Priority => 7;
    public override string Name => "!гифка";
    public override string Description => "выдаёт гифку по запрашиваемому тексту.";
    public string Parameters => "текст для поиска";

    protected override async Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        var imageUrl = await gifSearchService.SearchGifAsync(command.Parameters).ConfigureAwait(false);
        await SendMediaResponseAsync(imageUrl, command, false).ConfigureAwait(false);
        return imageUrl;
    }
}