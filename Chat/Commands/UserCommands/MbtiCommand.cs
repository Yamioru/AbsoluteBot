using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services.UserManagementServices;

namespace AbsoluteBot.Chat.Commands.UserCommands;

/// <summary>
///     Команда выдачи MBTI пользователя или привязки MBTI пользователю, если у него оно отсутствовало.
/// </summary>
public class MbtiCommand(MbtiService mbtiService) : BaseCommand, IParameterized
{
    public override int Priority => 301;
    public override string Description =>
        "привязывает или получает MBTI для пользователя.";
    public override string Name => "!mbti";
    public string Parameters => "4 буквы MBTI или никнейм";

    public override bool CanExecute(ParsedCommand command)
    {
        // Могут использовать не игнорируемые пользователи в официально подключенных чатах всех сервисов
        return CommandPermissionChecker.IsOfficialChannel(command);
    }

    protected override async Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        // Получение MBTI для указанного пользователя
        var userMbti = mbtiService.GetMbtiForUser(command.Parameters);
        if (userMbti != null) return $"MBTI пользователя {command.Parameters}: {userMbti}.";

        // Привязка MBTI к пользователю
        var mbti = command.Parameters.ToUpper();
        if (MbtiService.IsValidMbti(mbti))
        {
            if (await mbtiService.SetMbtiForUserAsync(command.Context.Username, mbti).ConfigureAwait(false))
                return $"MBTI {mbti} успешно привязан к пользователю {command.Context.Username}.";
            return "Не удалось привязать MBTI к пользователю.";
        }

        return $"Неправильный формат команды. Используйте: {Name} 4 буквы MBTI или {Name} никнейм. Пройти тест можно здесь: https://www.16personalities.com/ru/test-lichnosti";
    }
}