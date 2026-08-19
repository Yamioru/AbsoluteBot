using System.Text;
using AbsoluteBot.Services.UtilityServices;
using Newtonsoft.Json.Linq;
using Serilog;

namespace AbsoluteBot.Services.NeuralNetworkServices;

/// <summary>
///     Клиент Groq (OpenAI-compatible Chat Completions) со своим HttpClient.
/// </summary>
public class GroqChatService(ConfigService configService, HttpClient httpClient)
{
    public const string ChatCompletionsUrl = "https://api.groq.com/openai/v1/chat/completions";

    /// <summary>
    ///     Отправляет список сообщений в Groq и возвращает текст ответа.
    /// </summary>
    public virtual async Task<string?> CompleteAsync(IReadOnlyList<GroqChatMessage> messages, string model,
        double temperature = 1.0, int? maxTokens = null)
    {
        var apiKey = await configService.GetConfigValueAsync<string>("GroqApiKey").ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Log.Warning("Не настроен GroqApiKey.");
            return null;
        }

        if (string.IsNullOrWhiteSpace(model) || messages.Count == 0)
            return null;

        try
        {
            var payload = new JObject
            {
                ["model"] = model,
                ["temperature"] = temperature,
                ["messages"] = new JArray(messages.Select(m => new JObject
                {
                    ["role"] = m.Role,
                    ["content"] = m.Content
                }))
            };
            if (maxTokens is > 0)
                payload["max_tokens"] = maxTokens.Value;

            using var request = new HttpRequestMessage(HttpMethod.Post, ChatCompletionsUrl);
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + apiKey);
            request.Content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");

            var response = await httpClient.SendAsync(request).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                Log.Warning("Groq API вернул {Status}: {Body}", (int) response.StatusCode, Truncate(body));
                return null;
            }

            return ParseContent(body);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при запросе к Groq.");
            return null;
        }
    }

    private static string? ParseContent(string body)
    {
        var json = JObject.Parse(body);
        if (json["error"] != null)
        {
            Log.Warning("Groq API error: {Error}", json["error"]!.ToString());
            return null;
        }

        var contentToken = json["choices"]?[0]?["message"]?["content"];
        if (contentToken == null)
            return null;

        if (contentToken.Type == JTokenType.String)
            return contentToken.ToString();

        if (contentToken is JArray parts)
        {
            var texts = parts
                .Select(part => part["text"]?.ToString() ?? part["content"]?.ToString())
                .Where(text => !string.IsNullOrEmpty(text));
            var joined = string.Join(" ", texts);
            return string.IsNullOrWhiteSpace(joined) ? null : joined;
        }

        return contentToken.ToString();
    }

    private static string Truncate(string value) =>
        value.Length <= 500 ? value : value[..500] + "...";
}

/// <summary>
///     Сообщение в формате Groq/OpenAI chat completions.
/// </summary>
public readonly record struct GroqChatMessage(string Role, string Content);
