using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services.ChatServices.Interfaces;
using AbsoluteBot.Services.CommandManagementServices;

namespace AbsoluteBot.Chat.Commands.UserCommands;

/// <summary>
///     Команда для вывода всех команд доступных данному пользователю на данной платформе.
/// </summary>
public class CommandsListCommand(CommandsListService commandsListService) : IChatCommand
{
    public int Priority => 1000;
    public string Description => "выводит все доступные для пользователя команды.";
    public string Name => "!команды";

    public virtual bool CanExecute(ParsedCommand command)
    {
        // Могут использовать не игнорируемые пользователи
        return true;
    }

    public async Task<string> ExecuteAsync(ParsedCommand command)
    {
        // Проверка, нужно ли выполнить подготовку перед отправкой сообщения
        if (command.Context.ChatService is IMessagePreparationService preparationService)
            await preparationService.PrepareMessageAsync(command.Context).ConfigureAwait(false);

        string? response;

        // Выполнение логики через сервис
        if (command.Context.ChatService is not IMarkdownMessageService)
            response = await commandsListService.GenerateCommandsListAsImageAsync(command).ConfigureAwait(false);
        else
            response = commandsListService.GenerateCommandsListAsText(command);

        // Если не удалось получить текст списка команд
        if (string.IsNullOrEmpty(response))
        {
            command.Response = "Ошибка при генерации списка команд.";
            await command.Context.ChatService.SendMessageAsync(command.Response, command.Context).ConfigureAwait(false);
            return command.Response;
        }

        // Отправка списка команд
        switch (command.Context.ChatService)
        {
            case IMarkdownMessageService markdownMessageService:
                await markdownMessageService.SendMarkdownMessageAsync(response, command.Context).ConfigureAwait(false);
                break;
            case IUrlShorteningService urlShorteningService:
                await urlShorteningService.SendShortenedUrlAsync(response, command.Context).ConfigureAwait(false);
                break;
            default:
                await command.Context.ChatService.SendMessageAsync(response, command.Context).ConfigureAwait(false);
                break;
        }

        command.Response = response;
        return command.Response;
    }
}