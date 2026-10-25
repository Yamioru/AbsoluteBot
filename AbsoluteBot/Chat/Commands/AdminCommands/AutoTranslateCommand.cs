using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services.UtilityServices;

namespace AbsoluteBot.Chat.Commands.AdminCommands;

/// <summary>
///     Команда добавления пользователя в автоматический перевод его сообщений на русский язык.
/// </summary>
public class AutoTranslateCommand(AutoTranslateService autoTranslateService) : BaseCommand, IParameterized
{
    public override int Priority => 702;
    public override string Description => "начинает или перестаёт переводить все сообщения пользователя на русский язык.";
    public override string Name => "!перевод";
    public string Parameters => "никнейм";

    public override bool CanExecute(ParsedCommand command)
    {
        // Использовать могут модераторы и администраторы
        return command.UserRole is UserRole.Administrator or UserRole.Moderator;
    }

    protected override async Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        var username = command.Parameters;
        if (!await autoTranslateService.ToggleUserAutoTranslateAsync(username).ConfigureAwait(false))
            return "Не удалось переключить режим перевода у пользователя.";
        return autoTranslateService.IsUserAutoTranslating(username)
            ? $"{username} будет автоматически переводиться."
            : $"{username} больше не будет автоматически переводиться.";
    }
}