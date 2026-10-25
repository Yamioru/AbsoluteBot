using AbsoluteBot.Chat.Context;
using AbsoluteBot.Services.ChatServices.Interfaces;

namespace AbsoluteBot.Chat.Commands;

public abstract class BaseMediaCommand : BaseCommand
{
    public override async Task<string> ExecuteAsync(ParsedCommand command)
    {
        // Проверка, нужно ли выполнить подготовку перед отправкой сообщения
        if (command.Context.ChatService is IMessagePreparationService preparationService)
            await preparationService.PrepareMessageAsync(command.Context).ConfigureAwait(false);
        // Проверка содержит ли команда параметры,елс они нужны
        if (!HasRequiredParameters(ref command)) return command.Response!;

        // Выполнение специфической логики команды
        command.Response = await ExecuteLogicAsync(command).ConfigureAwait(false);
        return command.Response;
    }

    /// <summary>
    ///     Отправляет медиа контент разными способами в зависимости от возможной текущего ChatService.
    /// </summary>
    /// <param name="mediaUrl">Url медиа.</param>
    /// <param name="command">Команда.</param>
    /// <param name="isCanBeSendAsPhoto">Может ли данное медиа быть отправлено как фото.</param>
    protected static async Task SendMediaResponseAsync(string mediaUrl, ParsedCommand command, bool isCanBeSendAsPhoto = true)
    {
        command.Response = mediaUrl;
        switch (command.Context.ChatService)
        {
            case IPhotoSendingService photoSendingService when isCanBeSendAsPhoto:
                await photoSendingService.SendPhotoAsync(mediaUrl, command.Context).ConfigureAwait(false);
                break;
            case IDocumentSendingService documentSendingService:
                await documentSendingService.SendDocumentAsync(mediaUrl, command.Context).ConfigureAwait(false);
                break;
            case IUrlShorteningService urlShorteningService:
                await urlShorteningService.SendShortenedUrlAsync(mediaUrl, command.Context).ConfigureAwait(false);
                break;
            default:
                await command.Context.ChatService.SendMessageAsync(mediaUrl, command.Context).ConfigureAwait(false);
                break;
        }
    }
}