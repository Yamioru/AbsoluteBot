using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services;

namespace AbsoluteBot.Chat.Commands.UserCommands
{
    public class DivergenceCommand(DivergenceService divergenceService) : BaseCommand
    {
        public override int Priority => 408;
        public override string Description => "выдаёт текущее отклонение от исходной мировой линии";
        public override string Name => "!отклонение";
        public override bool CanExecute(ParsedCommand command)
        {
            // Могут использовать не игнорируемые пользователи не являющиеся ботами в официально подключенных чатах всех сервисов
            return command.UserRole is not (UserRole.Ignored or UserRole.Bot) && !CommandPermissionChecker.IsStreamingChannel(command);
        }

        protected override Task<string> ExecuteLogicAsync(ParsedCommand command)
        {
            return Task.FromResult("Текущее отклонение от исходной мировой линии: " + divergenceService.GetCurrentDivergence());
        }
    }
}
