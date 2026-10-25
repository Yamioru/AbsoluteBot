using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services.ChatServices.Interfaces;

namespace AbsoluteBot.Chat.Commands;

/// <summary>
///     Абстрактный базовый класс для реализации команд чата.
///     Предоставляет стандартную реализацию выполнения команд, включая проверку возможности выполнения, подготовку перед
///     отправкой сообщения и саму отправку текстового сообщения.
///     Наследуемые классы должны реализовать логику конкретной команды, определяя метод <see cref="ExecuteLogicAsync" />.
/// </summary>
public abstract class BaseCommand : IChatCommand
{
    public abstract string Name { get; }
    public abstract int Priority { get; }
    public abstract string Description { get; }

    public virtual bool CanExecute(ParsedCommand command)
    {
        // Могут использовать не игнорируемые пользователи
        return command.UserRole != UserRole.Ignored;
    }

    public virtual async Task<string> ExecuteAsync(ParsedCommand command)
    {
        // Проверка, нужно ли выполнить подготовку перед отправкой сообщения
        if (command.Context.ChatService is IMessagePreparationService preparationService)
            await preparationService.PrepareMessageAsync(command.Context).ConfigureAwait(false);

        // Проверка содержит ли команда параметры, если они нужны
        if (!HasRequiredParameters(ref command)) return command.Response!;

        // Выполнение специфической логики команды
        command.Response = await ExecuteLogicAsync(command).ConfigureAwait(false);

        // Отправка ответа в зависимости от платформы
        await command.Context.ChatService.SendMessageAsync(command.Response, command.Context).ConfigureAwait(false);
        return command.Response;
    }

    /// <summary>
    ///     Метод выполнения специфической логики команды.
    /// </summary>
    /// <param name="command">Команда</param>
    /// <returns>Результирующий текст выполнения логики, который будет отправлен в чат.</returns>
    protected abstract Task<string> ExecuteLogicAsync(ParsedCommand command);

    /// <summary>
    ///     Метод проверки наличия параметров команды, если они необходимы.
    /// </summary>
    /// <param name="command">Команда</param>
    /// <returns>True если параметры не нужны или присутствуют. False если необходимые параметры не найдены</returns>
    protected virtual bool HasRequiredParameters(ref ParsedCommand command)
    {
        if (this is not IParameterized parameterizedCommand || !string.IsNullOrEmpty(command.Parameters)) return true;
        command.Response = $"Использование: {Name} *{parameterizedCommand.Parameters}*";
        command.Context.ChatService.SendMessageAsync(command.Response, command.Context);
        return false;
    }
}