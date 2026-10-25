namespace AbsoluteBot.Services.NeuralNetworkServices;

/// <summary>
///     Надстройка одиночных запросов к нейросети (Gemini или Groq).
/// </summary>
public interface INeuralAskService
{
    /// <summary>
    ///     Обычный запрос (сущность Ask).
    /// </summary>
    Task<string?> AskAsync(string message, int maxLength, string? replyMessage = null, string? base64Image = null,
        string? instruction = null, double temperature = 1.0);

    /// <summary>
    ///     Запрос с поиском (сущность GoogleSearch).
    /// </summary>
    Task<string?> AskWithSearchAsync(string message, int maxLength, string? replyMessage = null, string? base64Image = null,
        string? instruction = null, double temperature = 1.0);

    /// <summary>
    ///     Одиночный запрос к указанной сущности (Chat/Ask/GoogleSearch).
    /// </summary>
    Task<string?> AskEntityAsync(string entity, string message, int maxLength, string instruction, double temperature = 0.2);
}
