using AbsoluteBot.Chat.Context;
using AbsoluteBot.Services.ChatServices.Interfaces;
using AbsoluteBot.Services.NeuralNetworkServices;
using Serilog;

namespace AbsoluteBot.Chat.Commands.UserCommands;

/// <summary>
///     Команда получения ответа на вопрос с помощью Gemini.
/// </summary>
public class AskGeminiCommand(AskGeminiService geminiService) : BaseCommand, IParameterized
{
    public override int Priority => 2;
    public override string Description => "выдаёт ответ на практически любой вопрос с помощью другой нейросети.";
    public override string Name => "!!спросить";
    public string Parameters => "вопрос";

    protected override async Task<string> ExecuteLogicAsync(ParsedCommand command)
    {
        // Логирование запроса с информацией о пользователе
        Log.Information("Gemini запрос от {Username} на платформе {Platform}: {Question}", 
            command.Context.Username, 
            command.Context.Platform, 
            command.Parameters);

        string? response;
        if (command.Context.ChatService is IChatImageService chatImageService)
        {
            var image = await chatImageService.GetImageAsBase64Async(command.Parameters, command.Context);
            response = await geminiService.AskGeminiResponseAsync(command.Parameters, command.Context.MaxMessageLength, command.Context.Reply?.Message, image).ConfigureAwait(false);
        }
        else
        {
            response = await geminiService.AskGeminiResponseAsync(command.Parameters, command.Context.MaxMessageLength, command.Context.Reply?.Message).ConfigureAwait(false);
        }

        // Логирование результата
        if (response != null)
        {
            Log.Information("Gemini успешно ответил пользователю {Username}", command.Context.Username);
        }
        else
        {
            Log.Warning("Gemini не смог ответить пользователю {Username} на запрос: {Question}", 
                command.Context.Username, 
                command.Parameters);
        }

        return response ?? "Не удалось получить ответ.";
    }
}