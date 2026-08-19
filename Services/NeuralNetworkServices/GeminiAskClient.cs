using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Serilog;

namespace AbsoluteBot.Services.NeuralNetworkServices;

/// <summary>
///     Клиент одиночных запросов к Gemini. Без знания о Groq.
/// </summary>
public class GeminiAskClient(GeminiSettingsProvider settingsProvider)
{
    public bool HasApiKeys => settingsProvider.ApiKeys is {Count: > 0};

    public async Task<string?> AskAsync(string message, string instruction, string? base64Image, string model,
        double temperature, bool enableGoogleSearch)
    {
        if (!HasApiKeys)
        {
            Log.Warning("Не настроены API ключи для Gemini.");
            return null;
        }

        var youtube = Regex.Match(message, @"https?:\/\/(www\.)?(youtube\.com\/(watch\?v=|shorts\/)|youtu\.be\/)[\w\-]+");
        var jsonData = GenerateJsonPayload(message, instruction, base64Image, youtube.Success ? youtube.Value : null,
            temperature, enableGoogleSearch);

        foreach (var apiKey in settingsProvider.ApiKeys!)
        {
            try
            {
                var url = GeminiSettingsProvider.BuildGenerateContentUrl(model, apiKey);
                var text = await settingsProvider.FetchModelResponseAsync(jsonData, url).ConfigureAwait(false);

                if (!string.IsNullOrEmpty(text))
                {
                    Log.Debug("Успешный запрос к Gemini с использованием API ключа (первые 10 символов): {ApiKeyPreview}...",
                        apiKey.Length > 10 ? apiKey[..10] : apiKey);
                    return text;
                }

                Log.Warning("API ключ вернул пустой ответ, пробуем следующий ключ.");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Ошибка при попытке запроса с API ключом (первые 10 символов): {ApiKeyPreview}..., пробуем следующий ключ.",
                    apiKey.Length > 10 ? apiKey[..10] : apiKey);
            }
        }

        Log.Error("Все API ключи Gemini ({KeyCount} шт.) не смогли обработать запрос.", settingsProvider.ApiKeys.Count);
        return null;
    }

    private static string GenerateJsonPayload(string message, string instruction, string? image, string? youtube,
        double temperature, bool enableGoogleSearch)
    {
        var parts = new JArray
        {
            new JObject {["text"] = message}
        };

        if (!string.IsNullOrEmpty(image))
            parts.Add(
                new JObject
                {
                    ["inlineData"] = new JObject
                    {
                        ["mimeType"] = "image/png",
                        ["data"] = image
                    }
                });

        if (!string.IsNullOrEmpty(youtube))
            parts.Add(
                new JObject
                {
                    ["file_data"] = new JObject
                    {
                        ["file_uri"] = youtube
                    }
                });

        var jsonPayload = new JObject
        {
            ["system_instruction"] = new JObject
            {
                ["parts"] = new JArray
                {
                    new JObject {["text"] = instruction}
                }
            },
            ["contents"] = new JArray
            {
                new JObject
                {
                    ["parts"] = parts
                }
            },
            ["generationConfig"] = new JObject
            {
                ["temperature"] = temperature
            }
        };

        if (enableGoogleSearch)
            jsonPayload["tools"] = new JArray
            {
                new JObject
                {
                    ["google_search"] = new JObject()
                }
            };

        return jsonPayload.ToString();
    }
}
