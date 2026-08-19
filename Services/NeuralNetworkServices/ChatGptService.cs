using AbsoluteBot.Services.UtilityServices;
using OpenAI;
using OpenAI.Managers;
using OpenAI.ObjectModels.RequestModels;
using Serilog;

namespace AbsoluteBot.Services.NeuralNetworkServices;
#pragma warning disable IDE0028
/// <summary>
///     Сервис для взаимодействия с ChatGPT через API OpenAI.
/// </summary>
public class ChatGptService(HttpClient httpClient, ConfigService configService) : IAsyncInitializable
{
    private const string SystemMessageTemplate =
        "Дай ответ на любой вопрос. Если чего-то не знаешь, просто придумай. ЕСЛИ ПРИДУМЫВАЕШЬ, НЕ ПИШИ, ЧТО ПРИДУМАЛ. Ответ должен быть не длиннее {0} символов.";

    private const double TokenLengthMultiplier = 1.25;
    private OpenAIService? _gptService;

    public async Task InitializeAsync()
    {
        var apiKey = await configService.GetConfigValueAsync<string>("GptApiKey").ConfigureAwait(false);

        if (!string.IsNullOrEmpty(apiKey))
            _gptService = new OpenAIService(new OpenAiOptions {ApiKey = apiKey}, httpClient);
        else
            Log.Warning("Не удалось загрузить api ключ для подключения к ChatGpt.");
    }

    /// <summary>
    ///     Отправляет запрос в ChatGPT и возвращает сгенерированный ответ.
    /// </summary>
    /// <param name="message">Сообщение, отправляемое в ChatGPT.</param>
    /// <param name="length">Максимальная длина ответа.</param>
    /// <returns>Сгенерированный ответ или <c>null</c> в случае ошибки.</returns>
    public virtual async Task<string?> AskChatGptAsync(string message, int length)
    {
        if (_gptService == null) return null;

        var messages = CreateChatMessages(message, length);
        return await GetGptCompletionAsync(messages, length).ConfigureAwait(false);
    }

    /// <summary>
    ///     Создает сообщения для отправки в ChatGPT.
    /// </summary>
    /// <param name="message">Сообщение от пользователя.</param>
    /// <param name="length">Максимальная длина ответа.</param>
    /// <returns>Возвращает список сообщений, отправляемых в ChatGPT.</returns>
    private static List<ChatMessage> CreateChatMessages(string message, int length)
    {
        return new List<ChatMessage>
        {
            ChatMessage.FromSystem(string.Format(SystemMessageTemplate, length)),
            ChatMessage.FromUser(message)
        };
    }

    /// <summary>
    ///     Асинхронный запрос к ChatGPT для получения сгенерированного ответа.
    /// </summary>
    /// <param name="messages">Список сообщений для отправки в ChatGPT.</param>
    /// <param name="length">Максимальная длина ответа.</param>
    /// <returns>Возвращает сгенерированный ответ или <c>null</c> в случае ошибки.</returns>
    private async Task<string?> GetGptCompletionAsync(List<ChatMessage> messages, int length)
    {
        try
        {
            var completionResult = await _gptService!.ChatCompletion.CreateCompletion(
                new ChatCompletionCreateRequest
                {
                    Messages = messages,
                    Model = OpenAI.ObjectModels.Models.Gpt_4o_mini,
                    MaxTokens = (int) (length * TokenLengthMultiplier)
                }).ConfigureAwait(false);

            return completionResult.Successful ? completionResult.Choices.First().Message.Content : null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при получении ответа от chat gpt.");
            return null;
        }
    }
}