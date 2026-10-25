using AbsoluteBot.Chat.Context;
using AbsoluteBot.Services;
using AbsoluteBot.Services.ChatServices.Interfaces;
using AbsoluteBot.Services.NeuralNetworkServices;

namespace AbsoluteBot.Chat.Commands.UserCommands;

internal class ImageGenerateCommand(ImageGenerationService imageGenerationService, TranslationService translationService) : IChatCommand,
    IParameterized
{
    public string Description => "генерирует картинку по запросу.";

    public async Task<string> ExecuteAsync(ParsedCommand command)
    {
        // Проверка, нужно ли выполнить подготовку перед отправкой сообщения
        if (command.Context.ChatService is IMessagePreparationService preparationService)
            await preparationService.PrepareMessageAsync(command.Context).ConfigureAwait(false);

        // Проверка содержит ли команда параметры,елс они нужны
        if (!HasRequiredParameters(ref command)) return command.Response!;

        var translatedPrompt = await translationService.TranslateTextAsync(command.Parameters, "EN");

        if (translatedPrompt == null)
        {
            await command.Context.ChatService.SendMessageAsync("Не удалось описать картинку.", command.Context).ConfigureAwait(false);
            return "Не удалось описать картинку.";
        }

        var base64Image = imageGenerationService.GenerateImage(translatedPrompt);

        if (base64Image == null)
        {
            await command.Context.ChatService.SendMessageAsync("Не удалось создать картинку.", command.Context).ConfigureAwait(false);
            return "Не удалось создать картинку.";
        }

        if (command.Context.ChatService is IPhotoSendingService photoSendingService)
        {
            await photoSendingService.SendPhotoBase64Async(base64Image, command.Context).ConfigureAwait(false);
            return "картинка";
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

    public string Name => "!сгенерировать";
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