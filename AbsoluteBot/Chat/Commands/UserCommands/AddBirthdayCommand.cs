using System.Globalization;
using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services.UserManagementServices;

namespace AbsoluteBot.Chat.Commands.UserCommands;

#pragma warning disable IDE0300
/// <summary>
///     Команда добавления своего дня рождения в список для оповещения ботом.
/// </summary>
public class AddBirthdayCommand(BirthdayService birthdayService) : BaseCommand, IParameterized
{
    public override int Priority => 304;
    public override string Description => "добавляет информацию о вашем дне рождения.";
    public override string Name => "!добавитьдр";
    public string Parameters => "день и месяц";

    public override bool CanExecute(ParsedCommand command)
    {
        // Могут использовать не игнорируемые пользователи в официально подключенных чатах всех сервисов
        return command.UserRole != UserRole.Ignored && CommandPermissionChecker.IsOfficialChannel(command);
    }

    protected override async Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        var context = command.Context;
        var platform = context.Platform;
        var username = context.Username;

        string[] formats = {"d M", "d MM", "d MMMM", "dd M", "dd MM", "dd MMMM"};
        if (!DateTime.TryParseExact(command.Parameters, formats, new CultureInfo("ru-RU"), DateTimeStyles.None, out var date))
            return
                "Ошибка: указан некорректный формат даты. Используйте числовой формат \"день месяц\" или \"день месяц (словом)\", например: \"23 сентября\" или \"23 09\".";
        date = new DateTime(DateTime.MinValue.Year, date.Month, date.Day);
        var formattedDate = date.ToString("d MMMM", new CultureInfo("ru-RU"));
        return await birthdayService.AddOrUpdateUserBirthday(username, platform, date, context.UserId)
            .ConfigureAwait(false)
            ? $"День рождения успешно добавлен: {formattedDate}. Уведомления включены на платформе {platform}."
            : "Не удалось добавить ваш день рождения.";
    }
}