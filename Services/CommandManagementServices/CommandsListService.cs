using System.Text;
using AbsoluteBot.Chat.Commands;
using AbsoluteBot.Chat.Commands.Registry;
using AbsoluteBot.Chat.Context;
using AbsoluteBot.Services.MediaServices;
using Serilog;

namespace AbsoluteBot.Services.CommandManagementServices;
#pragma warning disable IDE0305
/// <summary>
///     Сервис для генерации списка доступных команд.
/// </summary>
public class CommandsListService(ICommandRegistry commandRegistry, IImageGeneratorService imageGeneratorService,
    ExtraCommandsService extraCommandsService)
{
    private const int PriorityThreshold = 5;

    /// <summary>
    ///     Генерирует список доступных команд в виде изображения.
    /// </summary>
    /// <param name="command">Команда вывода всех команд, которая содержит информацию о контексте.</param>
    /// <returns>URL изображения со списком команд.</returns>
    public virtual async Task<string?> GenerateCommandsListAsImageAsync(ParsedCommand command)
    {
        try
        {
            // Генерация текстового списка команд
            var commandsListText = GenerateCommandsListAsText(command);
            if (string.IsNullOrEmpty(commandsListText)) return null;

            // Генерация изображения с текстом команд
            return await imageGeneratorService.GenerateCommandsImageAsync(commandsListText).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при генерации изображения списка команд.");
            return null;
        }
    }

    /// <summary>
    ///     Генерирует список доступных команд в виде строки.
    /// </summary>
    /// <param name="command">Команда вывода всех команд, которая содержит информацию о контексте.</param>
    /// <returns>Список доступных команд в виде строки.</returns>
    public virtual string? GenerateCommandsListAsText(ParsedCommand command)
    {
        try
        {
            // Получение списка доступных команд
            var availableCommands = GetAvailableCommands(command);

            var result = new StringBuilder();
            result.AppendLine("Список доступных команд:\n");

            // Добавление команд с разделением по приоритетам
            AppendCommandsWithPriorities(result, availableCommands, command);

            // Добавление динамических команд, если они доступны
            AppendDynamicCommands(result, command);

            return result.ToString();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при генерации текстового списка команд.");
            return null;
        }
    }

    /// <summary>
    ///     Добавляет команды в результат с учетом приоритетов для их группировки.
    /// </summary>
    /// <param name="result">Объект StringBuilder для формирования итогового списка команд.</param>
    /// <param name="availableCommands">Список доступных команд.</param>
    /// <param name="command">Команда, по которой определяется доступность других команд.</param>
    private static void AppendCommandsWithPriorities(StringBuilder result, List<IChatCommand> availableCommands,
        ParsedCommand command)
    {
        int? lastPriority = null;

        foreach (var cmd in availableCommands)
        {
            // Добавляет пустую строку, если изменился приоритет
            if (lastPriority.HasValue && Math.Abs(cmd.Priority - lastPriority.Value) > PriorityThreshold)
                result.AppendLine();

            // Добавляет команду в список с отформатированным текстом
            result.AppendLine(FormatCommandText(cmd, command));
            lastPriority = cmd.Priority;
        }
    }

    /// <summary>
    ///     Добавляет в список доступные динамические команды, если они есть.
    /// </summary>
    /// <param name="result">Объект StringBuilder для формирования итогового списка команд.</param>
    /// <param name="command">Команда, по которой определяется доступность динамических команд.</param>
    private void AppendDynamicCommands(StringBuilder result, ParsedCommand command)
    {
        var dynamic = commandRegistry.FindCommandByType<ExecuteExtraCommand>();
        if (dynamic == null || !dynamic.CanExecute(command)) return;
        var dynamicCommands = extraCommandsService.GetAllCommands();
        foreach (var dynamicCommand in dynamicCommands)
            result.AppendLine($"{command.Context.TextFormatter.FormatBold(dynamicCommand)}");
    }

    /// <summary>
    ///     Форматирует текст команды, добавляя жирный текст для имени команды и курсив для параметров (если они есть).
    /// </summary>
    /// <param name="cmd">Команда, которую нужно отформатировать.</param>
    /// <param name="command">Текущая команда, для которой создается список команд.</param>
    /// <returns>Отформатированная строка с именем и описанием команды.</returns>
    private static string FormatCommandText(IChatCommand cmd, ParsedCommand command)
    {
        if (cmd is IParameterized parameterizedCommand)
            return
                $"{command.Context.TextFormatter.FormatBold(cmd.Name)} {command.Context.TextFormatter.FormatItalic(parameterizedCommand.Parameters)} — {cmd.Description}";
        return $"{command.Context.TextFormatter.FormatBold(cmd.Name)} — {cmd.Description}";
    }

    /// <summary>
    ///     Получает список доступных команд, фильтруя по контексту выполнения команды.
    /// </summary>
    /// <param name="command">Команда, по которой определяется доступность других команд.</param>
    /// <returns>Список доступных команд.</returns>
    private List<IChatCommand> GetAvailableCommands(ParsedCommand command)
    {
        return commandRegistry.GetAllCommands()
            .Where(c => c.CanExecute(command))
            .OrderBy(c => c.Priority)
            .ThenBy(c => c.Name)
            .ToList();
    }
}