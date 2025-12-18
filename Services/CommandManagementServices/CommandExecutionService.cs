using AbsoluteBot.Chat.Commands;
using AbsoluteBot.Chat.Commands.Registry;
using AbsoluteBot.Chat.Context;
using AbsoluteBot.Messaging;
using AbsoluteBot.Models;
using AbsoluteBot.Services.UserManagementServices;

namespace AbsoluteBot.Services.CommandManagementServices;

/// <summary>
///     Сервис для выполнения команд, отправленных в чат.
/// </summary>
public class CommandExecutionService(ICommandParser commandParser, ICommandRegistry commandRegistry,
    RoleService roleService, CooldownService cooldownService, CommandStatusService commandStatusService)
{
    /// <summary>
    ///     Выполняет команду, переданную в виде текста, в соответствующем контексте.
    /// </summary>
    /// <param name="text">Текст команды.</param>
    /// <param name="context">Контекст чата, в котором была отправлена команда.</param>
    /// <returns>Результат выполнения команды или <c>null</c>, если команда не распознана.</returns>
    public async Task<string?> ExecuteCommandAsync(string text, ChatContext context)
    {
        // Получение роли пользователя
        var userRole = await roleService.GetUserRoleAsync(context.Username).ConfigureAwait(false);

        // Парсинг команды
        var parsedCommand = commandParser.Parse(text, context, userRole);
        if (parsedCommand == null) return null;

        // Выполнение команды, если она найдена
        return await ExecuteParsedCommandAsync(parsedCommand, context).ConfigureAwait(false);
    }

    /// <summary>
    ///     Проверяет, можно ли выполнить команду и включена ли она в конфигурации.
    /// </summary>
    /// <param name="command">Команда для выполнения.</param>
    /// <param name="parsedCommand">Распознанная команда.</param>
    /// <param name="context">Контекст чата, в котором была отправлена команда.</param>
    /// <returns>True, если команда доступна для выполнения, иначе False.</returns>
    private async Task<bool> CanExecuteCommand(IChatCommand command, ParsedCommand parsedCommand, ChatContext context)
    {
        return command.CanExecute(parsedCommand) &&
               await commandStatusService.IsCommandEnabled(command.Name, context.Platform).ConfigureAwait(false);
    }

    /// <summary>
    ///     Выполняет указанную команду и обновляет статус перезарядки.
    /// </summary>
    /// <param name="command">Команда для выполнения.</param>
    /// <param name="parsedCommand">Распознанная команда.</param>
    /// <param name="context">Контекст чата, в котором была отправлена команда.</param>
    /// <returns>Результат выполнения команды.</returns>
    private async Task<string?> ExecuteCommandAsync(IChatCommand command, ParsedCommand parsedCommand,
        ChatContext context)
    {
        // Проверка на перезарядку и отправка сообщения при необходимости
        if (IsOnCooldown(parsedCommand.UserRole, context)) return await HandleCooldownAsync(context).ConfigureAwait(false);
        cooldownService.SetLastUsed(context.Platform);
        return await command.ExecuteAsync(parsedCommand).ConfigureAwait(false);
    }

    /// <summary>
    ///     Выполняет команду, если она была успешно распознана и доступна.
    /// </summary>
    /// <param name="parsedCommand">Распознанная команда.</param>
    /// <param name="context">Контекст чата, в котором была отправлена команда.</param>
    /// <returns>Результат выполнения команды или <c>null</c>, если команда не найдена или недоступна.</returns>
    private async Task<string?> ExecuteParsedCommandAsync(ParsedCommand parsedCommand, ChatContext context)
    {
        //Проверка на наличие динамических команд
        IChatCommand? command = commandRegistry.FindCommandByType<ExecuteExtraCommand>();
        if (command != null && command.CanExecute(parsedCommand))
            return await ExecuteCommandAsync(command, parsedCommand, context).ConfigureAwait(false);

        // Если команда не найдена, проверка на наличие обычных команд
        command = commandRegistry.FindCommand(parsedCommand.Command);
        if (command != null && await CanExecuteCommand(command, parsedCommand, context).ConfigureAwait(false))
            return await ExecuteCommandAsync(command, parsedCommand, context).ConfigureAwait(false);

        // Если команда не найдена, проверка на наличие всемогущих команд
        command = commandRegistry.FindCommandByType<AlmightyCommand>();
        if (command != null && command.CanExecute(parsedCommand) && parsedCommand.Command.Length > 1)
        {
            return await ExecuteCommandAsync(command, parsedCommand, context).ConfigureAwait(false);
        }
        return null;
    }

    /// <summary>
    ///     Обрабатывает сценарий, когда бот находится на перезарядке и отправляет сообщение об этом.
    /// </summary>
    /// <param name="context">Контекст чата, в котором была отправлена команда.</param>
    /// <returns>Сообщение о том, что бот на перезарядке.</returns>
    private static async Task<string> HandleCooldownAsync(ChatContext context)
    {
        const string message = "Бот на перезарядке. Подождите немного.";
        await context.ChatService.SendMessageAsync(message, context).ConfigureAwait(false);
        return message;
    }

    /// <summary>
    ///     Проверяет, находится ли бот на перезарядке для данного пользователя.
    /// </summary>
    /// <param name="userRole">Роль пользователя, отправившего команду.</param>
    /// <param name="context">Контекст чата, в котором была отправлена команда.</param>
    /// <returns>True, если бот на перезарядке, иначе False.</returns>
    private bool IsOnCooldown(UserRole userRole, ChatContext context)
    {
        return cooldownService.IsOnCooldown(context.Platform) &&
               userRole != UserRole.Premium &&
               userRole != UserRole.Administrator &&
               userRole != UserRole.Bot &&
               userRole != UserRole.Moderator;
    }
}