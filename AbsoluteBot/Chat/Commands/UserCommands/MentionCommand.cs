using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services;
using AbsoluteBot.Services.ChatServices.Interfaces;
using AbsoluteBot.Services.CommandManagementServices;
using AbsoluteBot.Services.NeuralNetworkServices;
using AbsoluteBot.Services.UtilityServices;
using Serilog;

namespace AbsoluteBot.Chat.Commands.UserCommands;

/// <summary>
///     Команда для ответа на сообщение упоминающее бота в чате.
/// </summary>
public class MentionCommand(INeuralChatService neuralChat, CommandExecutionService commandExecutionService, ConfigService configService) :
    IChatCommand,
    IParameterized, IAsyncInitializable
{
    private string? _botName;

    public async Task InitializeAsync()
    {
        _botName = await configService.GetConfigValueAsync<string>("BotName").ConfigureAwait(false);
        if (string.IsNullOrEmpty(_botName)) Log.Warning("Не удалось загрузить общее имя бота.");
    }

    public bool CanExecute(ParsedCommand command)
    {
        // Могут использовать не игнорируемые пользователи не являющиеся ботами в официально подключенных чатах всех сервисов
        return command.UserRole is not (UserRole.Ignored or UserRole.Bot) && CommandPermissionChecker.IsOfficialChannel(command);
    }

    public string Description => "поговорить с ботом.";
    public string Name => $"@{_botName}";
    public int Priority => 0;

    public async Task<string> ExecuteAsync(ParsedCommand command)
    {
        if (_botName == null) return "Прости, не могу сейчас говорить, давай позже.";

        // Проверка, нужно ли выполнить подготовку перед отправкой сообщения
        if (command.Context.ChatService is IMessagePreparationService preparationService)
            await preparationService.PrepareMessageAsync(command.Context).ConfigureAwait(false);

        var context = command.Context;
        string? response;

        // Если упоминание стриггерено случайно, то сообщение от бота получается на основе контекста беседы.
        if (context.LastMessages?.Count > 9)
        {
            response = await neuralChat.ChatAsync(context.LastMessages, context.Platform).ConfigureAwait(false);
        }
        else
        {
            var input = $"{context.Username}: {command.Parameters}";
            if (context.ChatService is IChatImageService chatImageService)
            {
                var image = await chatImageService.GetImageAsBase64Async(command.Parameters, context);
                response = await neuralChat.ChatAsync(input, context.Reply, context.Platform, image).ConfigureAwait(false);
            }
            else
            {
                response = await neuralChat.ChatAsync(input, context.Reply, context.Platform).ConfigureAwait(false);
            }
        }

        // Отправка ответа бота в чат
        command.Response = string.IsNullOrEmpty(response) ? "Прости, не могу сейчас говорить, давай позже." : response;
        await context.ChatService.SendMessageAsync(command.Response, context).ConfigureAwait(false);

        // Если бот ответил командой, ты эта команда выполняется и записывается в историю сообщений
        context.Username = _botName;
        response = await commandExecutionService.ExecuteCommandAsync(command.Response, context).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(response))
            await neuralChat.AddUserMessageToChatHistoryOnPlatform(response, _botName, command.Context.Platform).ConfigureAwait(false);
        response ??= command.Response;

        return response;
    }

    public string Parameters => "сообщение";
}