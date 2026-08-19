using AbsoluteBot.Models;

namespace AbsoluteBot.Services.NeuralNetworkServices;

/// <summary>
///     Надстройка диалогового чата с нейросетью (Gemini или Groq): история, платформы, генерация ответа.
/// </summary>
public interface INeuralChatService
{
    Task<bool> AddUserMessageToChatHistory(string message, string user);

    Task<bool> AddUserMessageToChatHistoryOnPlatform(string message, string user, string platform);

    Task<string?> ChatAsync(string userMessage, ReplyInfo? replyInfo, string platform, string? base64Image = null);

    Task<string?> ChatAsync(IEnumerable<string> userMessages, string platform);

    Task<bool> ClearPlatformChatHistoryAsync(string platform);
}
