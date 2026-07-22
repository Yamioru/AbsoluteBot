using AbsoluteBot.Chat.Context;
using AbsoluteBot.Services.UserManagementServices;

namespace AbsoluteBot.Chat.Commands.UserCommands;

public class SbtiCommand(SbtiService sbtiService) : BaseCommand, IParameterized
{
    public override int Priority => 301;
    public override string Description => "привязывает или получает SBTI (мемный тип личности) для пользователя.";
    public override string Name => "!sbti";
    public string Parameters => "Код SBTI (например: NPC, BOSS, JOKE-R) или никнейм";

    public override bool CanExecute(ParsedCommand command) => CommandPermissionChecker.IsOfficialChannel(command);

    protected override async Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        var userSbti = sbtiService.GetSbtiForUser(command.Parameters);
        if (userSbti != null)
        {
            var sbtiDescription = SbtiService.GetSbtiDescription(userSbti);
            return $"SBTI пользователя {command.Parameters}: {userSbti}. {sbtiDescription}";
        }

        var sbti = command.Parameters.ToUpper();
        if (SbtiService.IsValidSbti(sbti))
        {
            if (await sbtiService.SetSbtiForUserAsync(command.Context.Username, sbti).ConfigureAwait(false))
                return $"Тип SBTI '{sbti}' успешно привязан к пользователю {command.Context.Username}.";
            return "Не удалось привязать SBTI к пользователю.";
        }

        return $"Неправильный формат. Используйте: {Name} [Код] или {Name} [Никнейм]. Пройти тест можно здесь: https://sobekatypeb.github.io/SBTI/";
    }
}