using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services.ChatServices.Interfaces;

namespace AbsoluteBot.Chat.Commands.AdminCommands;

/// <summary>
///     Команда удаления последних сообщений в чате.
/// </summary>
public class DeleteMessagesCommand : BaseCommand, IParameterized
{
    public override int Priority => 100;
    public override string Description => "удаляет до 15 последних сообщений в чате..";
    public override string Name => "!удалить";
    public string Parameters => "число";

    public override bool CanExecute(ParsedCommand command)
    {
        // Использовать могут администраторы и бот
        return command.UserRole == UserRole.Administrator;
    }

    protected override async Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        const int maxMessageDeletionCount = 15;
        if (command.Context.ChatService is not ISupportsMessageDeletion deletionService)
            return "Эта платформа не поддерживает удаление сообщений.";

        if (!int.TryParse(command.Parameters, out var count)) return $"Использование: {Name} *число*";
        count = Math.Min(count, maxMessageDeletionCount);

        if (count <= 0) return "Число должно быть положительным.";

        await deletionService.DeleteMessagesAsync(command.Context, count).ConfigureAwait(false);
        return $"Удалено {count} последних сообщений.";
    }
}