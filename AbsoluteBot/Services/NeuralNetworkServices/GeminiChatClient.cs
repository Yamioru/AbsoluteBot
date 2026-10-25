using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace AbsoluteBot.Services.NeuralNetworkServices;

/// <summary>
///     Клиент диалоговых запросов к Gemini. Без знания о Groq.
/// </summary>
public class GeminiChatClient(GeminiSettingsProvider settingsProvider)
{
    private const int DelayBetweenAttempts = 500;
    private const string CategorySexuallyExplicit = "HARM_CATEGORY_SEXUALLY_EXPLICIT";
    private const string CategoryHateSpeech = "HARM_CATEGORY_HATE_SPEECH";
    private const string CategoryHarassment = "HARM_CATEGORY_HARASSMENT";
    private const string CategoryDangerousContent = "HARM_CATEGORY_DANGEROUS_CONTENT";
    private const string ThresholdBlockNone = "BLOCK_NONE";

    public bool HasApiKeys => settingsProvider.ApiKeys is {Count: > 0};

    public async Task<string?> CompleteAsync(ChatHistory chatHistory, Dictionary<string, string> replacements,
        double temperature, string model, int maxOutputTokens)
    {
        if (settingsProvider.ApiKeys == null) return null;

        var jsonData = GenerateJsonDataString(temperature, chatHistory, replacements, maxOutputTokens);
        foreach (var apiKey in settingsProvider.ApiKeys)
        {
            var url = GeminiSettingsProvider.BuildGenerateContentUrl(model, apiKey);
            try
            {
                var text = await settingsProvider.FetchModelResponseAsync(jsonData, url).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(text))
                    return text;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Попытка использования API-ключа и модели {Model} завершилась неудачей.", model);
            }
            finally
            {
                await Task.Delay(DelayBetweenAttempts).ConfigureAwait(false);
            }
        }

        return null;
    }

    private static string GenerateJsonDataString(double temperature, ChatHistory chatHistory,
        Dictionary<string, string> replacements, int maxOutputTokens)
    {
        var jsonData = new JObject
        {
            ["contents"] = chatHistory.GetHistory(replacements),
            ["generationConfig"] = new JObject
            {
                ["temperature"] = temperature,
                ["maxOutputTokens"] = maxOutputTokens
            },
            ["safetySettings"] = new JArray
            {
                new JObject {["category"] = CategorySexuallyExplicit, ["threshold"] = ThresholdBlockNone},
                new JObject {["category"] = CategoryHateSpeech, ["threshold"] = ThresholdBlockNone},
                new JObject {["category"] = CategoryHarassment, ["threshold"] = ThresholdBlockNone},
                new JObject {["category"] = CategoryDangerousContent, ["threshold"] = ThresholdBlockNone}
            }
        };
        return jsonData.ToString(Formatting.None);
    }
}
