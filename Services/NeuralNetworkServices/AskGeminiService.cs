using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Serilog;

namespace AbsoluteBot.Services.NeuralNetworkServices;

/// <summary>
/// Сервис для работы с моделью Gemini, поддерживающий генерацию ответов на основе запроса пользователя.
/// </summary>
public class AskGeminiService(GeminiSettingsProvider settingsProvider)
{
    /// <summary>
    /// Асинхронный запрос к модели Gemini с передачей сообщения.
    /// </summary>
    /// <param name="message">Сообщение для отправки модели.</param>
    /// <param name="maxLength">Максимальная длина ответа в символах.</param>
    /// <param name="base64Image">Изображение в формате Base64.</param>
    /// <returns>Ответ модели или null, если возникла ошибка.</returns>
    public async Task<string?> AskGeminiResponseAsync(string message, int maxLength, string? replyMessage = null, string? base64Image = null,
        string instruction = null, double temperature = 1.0)
    {
        if (settingsProvider.ApiKeys == null || settingsProvider.ApiKeys.Count == 0)
        {
            Log.Warning("Не настроены API ключи для Gemini.");
            return null;
        }

        if (string.IsNullOrEmpty(instruction))
        {
            instruction =
                $"Дай ответ на любой вопрос. СПОЙЛЕРЫ ПИСАТЬ СТРОГО ЗАПРЕЩЕНО. Если чего-то не знаешь, просто придумай. ЕСЛИ ПРИДУМЫВАЕШЬ, НЕ ПИШИ, ЧТО ПРИДУМАЛ. Постарайся уложиться в {maxLength} символов. Выдай цельный ответ без форматирования и без Markdown.";
            if (!string.IsNullOrEmpty(replyMessage))
                instruction += $". Следует учитывать данное сообщение в контексте вопроса пользователя: {replyMessage}";
        }

        var youtube = Regex.Match(message, @"https?:\/\/(www\.)?(youtube\.com\/(watch\?v=|shorts\/)|youtu\.be\/)[\w\-]+");
        var jsonData = GenerateJsonPayload(message, instruction, base64Image, youtube.Success ? youtube.Value : null);

        // Перебираем все доступные API ключи
        foreach (var apiKey in settingsProvider.ApiKeys)
        {
            try
            {
                var url = $"{GeminiSettingsProvider.BaseApiUrl}/{settingsProvider.Models[1]}:generateContent?key={apiKey}";
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

        // Если все ключи не сработали
        Log.Error("Все API ключи Gemini ({KeyCount} шт.) не смогли обработать запрос.", settingsProvider.ApiKeys.Count);
        return null;
    }

    /// <summary>
    /// Создание JSON-данных с сообщением и опциональной строкой изображения для отправки модели.
    /// </summary>
    /// <param name="message">Сообщение пользователя.</param>
    /// <param name="instruction">Инструкция для нейронной сети</param>
    /// <param name="image">Строка, представляющая изображение в формате Base64. Если не указана, отправляется только текст.</param>
    /// <returns>JSON-данные для отправки модели.</returns>
    private static string GenerateJsonPayload(string message, string instruction, string? image = null, string? youtube = null, double temperature = 1.0)
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
            ["tools"] = new JArray
            {
                new JObject
                {
                    ["google_search"] = new JObject()
                }
            },
            ["generationConfig"] = new JObject
            {
                ["temperature"] = temperature
            }
        };

        return jsonPayload.ToString();
    }
}