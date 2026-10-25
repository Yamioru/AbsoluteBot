using AbsoluteBot.Chat.Context;
using AbsoluteBot.Services.MediaServices;

namespace AbsoluteBot.Chat.Commands.UserCommands;

/// <summary>
///     Команда для получения картинки прикольного котика.
/// </summary>
public class CatCommand(CatImageService catImageService) : BaseMediaCommand
{
    public override int Priority => 403;
    public override string Name => "!котик";
    public override string Description => "присылает картинку забавного котика.";

    protected override async Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        var imageUrl = await catImageService.GetCatImageAsync().ConfigureAwait(false);
        await SendMediaResponseAsync(imageUrl, command).ConfigureAwait(false);
        return imageUrl;
    }
}