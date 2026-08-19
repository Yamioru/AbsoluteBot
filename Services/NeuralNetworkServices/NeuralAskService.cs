namespace AbsoluteBot.Services.NeuralNetworkServices;

/// <summary>
///     Надстройка одиночных запросов: выбирает Gemini или Groq по <see cref="NeuralModelConfigService" />.
/// </summary>
public class NeuralAskService(
    NeuralModelConfigService modelConfig,
    GeminiAskClient geminiAsk,
    GroqChatService groqService) : INeuralAskService
{
    public Task<string?> AskAsync(string message, int maxLength, string? replyMessage = null, string? base64Image = null,
        string? instruction = null, double temperature = 1.0) =>
        AskCoreAsync(NeuralEntities.Ask, message, maxLength, replyMessage, base64Image, instruction, temperature, false);

    public Task<string?> AskWithSearchAsync(string message, int maxLength, string? replyMessage = null,
        string? base64Image = null, string? instruction = null, double temperature = 1.0) =>
        AskCoreAsync(NeuralEntities.GoogleSearch, message, maxLength, replyMessage, base64Image, instruction, temperature, true);

    private async Task<string?> AskCoreAsync(string entity, string message, int maxLength, string? replyMessage,
        string? base64Image, string? instruction, double temperature, bool enableGeminiGoogleSearch)
    {
        if (string.IsNullOrEmpty(instruction))
        {
            instruction =
                $"Дай ответ на любой вопрос. СПОЙЛЕРЫ ПИСАТЬ СТРОГО ЗАПРЕЩЕНО. Если чего-то не знаешь, просто придумай. ЕСЛИ ПРИДУМЫВАЕШЬ, НЕ ПИШИ, ЧТО ПРИДУМАЛ. Постарайся уложиться в {maxLength} символов. Выдай цельный ответ без форматирования и без Markdown.";
            if (!string.IsNullOrEmpty(replyMessage))
                instruction += $". Следует учитывать данное сообщение в контексте вопроса пользователя: {replyMessage}";
        }

        var model = modelConfig.GetModel(entity);
        if (string.Equals(modelConfig.GetProvider(), NeuralProviders.Groq, StringComparison.OrdinalIgnoreCase))
        {
            var messages = new List<GroqChatMessage>
            {
                new("system", instruction),
                new("user", message)
            };
            return await groqService.CompleteAsync(messages, model, temperature).ConfigureAwait(false);
        }

        return await geminiAsk.AskAsync(message, instruction, base64Image, model, temperature, enableGeminiGoogleSearch)
            .ConfigureAwait(false);
    }
}
