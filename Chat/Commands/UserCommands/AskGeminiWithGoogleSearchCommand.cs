using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services.ChatServices.Interfaces;
using AbsoluteBot.Services.NeuralNetworkServices;
using Serilog;

namespace AbsoluteBot.Chat.Commands.UserCommands;

/// <summary>
/// Команда получения ответа на вопрос с помощью Gemini.
/// </summary>
public class AskGeminiWithGoogleSearchCommand(AskGeminiService geminiService) : IChatCommand, IParameterized
{
    public int Priority => 2;
    public string Description => "гуглит информацию в интернете и выдаёт.";
    public string Name => "!загугли";
    public string Parameters => "вопрос";

    public virtual bool CanExecute(ParsedCommand command)
    {
        // Могут использовать не игнорируемые пользователи
        return command.UserRole != UserRole.Ignored;
    }

    public async Task<string> ExecuteAsync(ParsedCommand command)
    {
        // Логирование запроса с информацией о пользователе
        Log.Information("Gemini Google Search запрос от {Username} на платформе {Platform}: {Question}", 
            command.Context.Username, 
            command.Context.Platform, 
            command.Parameters);

        const string prompt = "Загугли пожалуйста: ";
        string? response;
        if (command.Context.ChatService is IChatImageService chatImageService)
        {
            var image = await chatImageService.GetImageAsBase64Async(command.Parameters, command.Context);
            response = await geminiService
                .AskGeminiResponseAsync(prompt + command.Parameters, command.Context.MaxMessageLength, command.Context.Reply?.Message, image)
                .ConfigureAwait(false);
        }
        else
        {
            response = await geminiService
                .AskGeminiResponseAsync(prompt + command.Parameters, command.Context.MaxMessageLength, command.Context.Reply?.Message)
                .ConfigureAwait(false);
        }

        // Логирование результата
        if (response != null)
        {
            Log.Information("Gemini Google Search успешно ответил пользователю {Username}", command.Context.Username);
        }
        else
        {
            Log.Warning("Gemini Google Search не смог ответить пользователю {Username} на запрос: {Question}", 
                command.Context.Username, 
                command.Parameters);
        }

        response ??= "Не удалось получить ответ.";
        switch (command.Context.ChatService)
        {
            case IMarkdownMessageService markdownMessageService:
                await markdownMessageService.SendMarkdownMessageAsync(response, command.Context).ConfigureAwait(false);
                break;
            default:
                await command.Context.ChatService.SendMessageAsync(response, command.Context).ConfigureAwait(false);
                break;
        }
        command.Response = response;
        return command.Response;
    }
}