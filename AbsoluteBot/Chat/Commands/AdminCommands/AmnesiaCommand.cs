using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services.NeuralNetworkServices;

namespace AbsoluteBot.Chat.Commands.AdminCommands;

/// <summary>
///     Команда очистки истории сообщений у бота в диалоговом режиме чата с использованием Gemini.
/// </summary>
public class AmnesiaCommand(INeuralChatService neuralChat) : BaseCommand
{
    public override int Priority => 706;
    public override string Description => "удаляет воспоминания чат бота.";
    public override string Name => "!амнезия";

    public override bool CanExecute(ParsedCommand command)
    {
        // Использовать могут администраторы и бот
        return command.UserRole is UserRole.Administrator or UserRole.Bot;
    }

    protected override async Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        return await neuralChat.ClearPlatformChatHistoryAsync(command.Context.Platform).ConfigureAwait(false)
            ? "Команда успешно применена."
            : "Не удалось применить команду.";
    }
}