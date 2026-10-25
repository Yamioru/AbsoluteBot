using AbsoluteBot.Chat.Context;
using AbsoluteBot.Services;

namespace AbsoluteBot.Chat.Commands.UserCommands;

/// <summary>
///     Команда получения сегодняшнего праздника
/// </summary>
public class HolidayCommand(HolidaysService holidaysService) : BaseCommand
{
    public override int Priority => 406;
    public override string Description => "выдаёт сегодняшний праздник.";
    public override string Name => "!праздник";

    protected override Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        var today = DateTime.Now.ToString("dd.MM");
        var holiday = holidaysService.GetHoliday(today);
        return Task.FromResult(holiday);
    }
}