using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services.ChatServices.VkPlayLive;
using AbsoluteBot.Services.NeuralNetworkServices;

namespace AbsoluteBot.Chat.Commands;

/// <summary>
/// Класс всемогущих команд.
/// </summary>
public class AlmightyCommand(AskGeminiService askGeminiService) : IChatCommand
{
    public string Name => "Всемогущие команды";
    public int Priority => 1200;
    public string Description => "может выполнить всё.";

    public bool CanExecute(ParsedCommand command)
    {
        // Может быть использована если:
        return command.UserRole != UserRole.Ignored // не игнорируемые пользователи
               && CommandPermissionChecker.IsOfficialChannel(command) // в официально подключенных чатах всех сервисов
               && command.Context.ChatService is not VkPlayChatService
               && command.Command.StartsWith('!'); 
    }

    public async Task<string> ExecuteAsync(ParsedCommand command)
    {
        var message = "Представь, что ты что-то типо Siri или Алисы из Apple и Яндекс соответственно, такой искусственный интеллект, который может помочь человеку выполнить любую его команду, начиная от записи к врачу, заканчивая уборкой по дому. Считай, что ты всесильная, даже если какую-то ты думаешь, что выполнить не реально, подыграй и сделай вид, что сделала её." +
                         " Вот команда, которую запросил пользовать:\"" + command.Command + "\"" + (string.IsNullOrEmpty(command.Parameters) ? "" : ", а вот тело команды:" + command.Parameters);
        command.Response = await askGeminiService.AskGeminiResponseAsync(message, command.Context.MaxMessageLength).ConfigureAwait(false) ?? "Не удалось выполнить команду.";
        await command.Context.ChatService.SendMessageAsync(command.Response, command.Context).ConfigureAwait(false);
        return command.Response;
    }
}