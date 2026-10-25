using AbsoluteBot.Chat.Context;
using AbsoluteBot.Services.MediaServices;

namespace AbsoluteBot.Chat.Commands.UserCommands;

/// <summary>
///     Команда получения картинки человека похожего на заданного человека.
/// </summary>
public class LikenessCommand(ImageSearchService imageSearchService) : BaseMediaCommand, IParameterized
{
    public override int Priority => 404;
    public override string Name => "!сходство";
    public override string Description => "выдаёт картинку похожей на известного запрашиваемого человека знаменитости.";
    public string Parameters => "имя человека";

    protected override async Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        var imageUrl = await imageSearchService.SearchImageAsync(command.Parameters + "+likeness.ru").ConfigureAwait(false);
        await SendMediaResponseAsync(imageUrl, command).ConfigureAwait(false);
        return imageUrl;
    }
}