using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;

namespace AbsoluteBot.Chat.Commands.AdminCommands;

/// <summary>
///     Команда для установки напоминания через заданное время в минутах с отправкой указанного текста.
/// </summary>
public class RemindCommand : BaseCommand, IParameterized
{
    public override int Priority => 700;
    public override string Name => "!напомнить";
    public override string Description => "присылает через заданное количество минут указанное сообщение";
    public string Parameters => "время в минутах и сообщение";

    public override bool CanExecute(ParsedCommand command)
    {
        // Использовать могут модераторы и администраторы
        return command.UserRole is UserRole.Administrator or UserRole.Moderator;
    }

    protected override async Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        // Разделение параметров: ожидание времени и текст сообщения
        var parameters = command.Parameters.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parameters.Length < 2 || !int.TryParse(parameters[0], out var minutes)) return $"Использование: {Name} {Parameters}";

        var remindText = parameters[1];

        // Ответ пользователю о принятии команды
        await command.Context.ChatService.SendMessageAsync($"Напоминание будет отправлено через {minutes} минут(ы).", command.Context).ConfigureAwait(false);

        // Задержка на указанное количество минут
        await Task.Delay(TimeSpan.FromMinutes(minutes)).ConfigureAwait(false);

        // Отправка напоминания
        var reminderMessage = $"Напоминание: {remindText}";
        return reminderMessage;
    }
}