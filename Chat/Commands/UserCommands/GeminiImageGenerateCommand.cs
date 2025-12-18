using AbsoluteBot.Chat.Context;
using AbsoluteBot.Services.ChatServices.Interfaces;
using AbsoluteBot.Services.NeuralNetworkServices;

namespace AbsoluteBot.Chat.Commands.UserCommands;

internal class GeminiImageGenerateCommand(GeminiImageGenerationService imageGenerationService) : IChatCommand,
    IParameterized
{
    public string Description => "качественно генерирует картинку по запросу.";

    public async Task<string> ExecuteAsync(ParsedCommand command)
    {
        // Проверка, нужно ли выполнить подготовку перед отправкой сообщения
        if (command.Context.ChatService is IMessagePreparationService preparationService)
            await preparationService.PrepareMessageAsync(command.Context).ConfigureAwait(false);

        // Проверка содержит ли команда параметры,елс они нужны
        if (!HasRequiredParameters(ref command)) return command.Response!;
        string? text;
        string? base64Image;
        if (command.Context.ChatService is IChatImageService chatImageService)
        {
            var image = await chatImageService.GetImageAsBase64Async(command.Parameters, command.Context);
            (text, base64Image) = await imageGenerationService.GenerateImageGeminiResponseAsync(command.Parameters, image);
        }
        else
        {
            (text, base64Image) = await imageGenerationService.GenerateImageGeminiResponseAsync(command.Parameters);
        }

        if (string.IsNullOrEmpty(base64Image))
        {
            if (string.IsNullOrEmpty(text))
            {
                await command.Context.ChatService.SendMessageAsync("Не удалось создать картинку.", command.Context).ConfigureAwait(false);
                return "Не удалось создать картинку.";
            }

            await command.Context.ChatService.SendMessageAsync(text, command.Context).ConfigureAwait(false);
            return text;
        }

        if (command.Context.ChatService is IPhotoSendingService photoSendingService)
        {
            await photoSendingService.SendPhotoBase64Async(base64Image, command.Context).ConfigureAwait(false);
            if (string.IsNullOrEmpty(text))
            {
                return "картинка";
            }

            await command.Context.ChatService.SendMessageAsync(text, command.Context).ConfigureAwait(false);
            return text;
        }

        await command.Context.ChatService.SendMessageAsync("Не удалось создать картинку.", command.Context).ConfigureAwait(false);
        return "картинка";
    }

    public bool CanExecute(ParsedCommand command)
    {
        // Использовать можно только в административных каналах или в премиумном телеграме
        return CommandPermissionChecker.IsAdministrativeChannel(command) ||
               command.Context is TelegramChatContext {ChannelType: ChannelType.Premium};
    }

    public string Name => "!нарисуй";
    public int Priority => 5;
    public string Parameters => "текст запроса";

    /// <summary>
    /// Метод проверки наличия параметров команды, если они необходимы.
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