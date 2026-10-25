using AbsoluteBot.Chat.Context;
using AbsoluteBot.Services.MediaServices;

namespace AbsoluteBot.Chat.Commands.UserCommands;

/// <summary>
///     Команда поиска и выдачи видео с ютуба по запрашиваемому тексту.
/// </summary>
public class VideoCommand(VideoSearchService videoSearchService) : BaseCommand, IParameterized
{
    public override int Priority => 8;
    public override string Description => "выдаёт видео с ютуба по тексту запроса.";
    public override string Name => "!видео";
    public string Parameters => "текст запроса";

    protected override async Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        return await videoSearchService.SearchVideoAsync(command.Parameters).ConfigureAwait(false);
    }
}