using System.Text;
using Newtonsoft.Json.Linq;
using Serilog;

namespace AbsoluteBot.Services.NeuralNetworkServices;

/// <summary>
///     Устаревший сервис генерации/правки картинок через Gemini.
///     Живая команда <c>!нарисуй</c> использует <see cref="CloudflareFluxImageService"/>.
///     Оставлен на случай, если Google снова откроет image API на бесплатных ключах.
/// </summary>
[Obsolete("Живая команда !нарисуй идёт через CloudflareFluxImageService. Оставлен как запасной путь для Gemini image API.")]
public class GeminiImageGenerationService(
    GeminiSettingsProvider settingsProvider,
    NeuralModelConfigService modelConfig,
    HttpClient httpClient)
{
    /// <summary>
    /// Сгенерировать/отредактировать изображение.
    /// Если передать base64 изображения, модель выполнит редактирование (image+text->image).
    /// </summary>
    /// <param name="message">Текстовый промпт.</param>
    /// <param name="base64Image">Опционально: входное изображение base64 (без префикса data:...)</param>
    /// <returns>(text, imageBase64) — текст и первая полученная картинка в base64, либо null/null</returns>
    public async Task<(string? text, string? image)> GenerateImageGeminiResponseAsync(string message, string? base64Image = null)
    {
        try
        {
            if (settingsProvider.ApiKeys == null || settingsProvider.ApiKeys.Count == 0)
                return (null, null);

            // Немного «подсказки» модели на русском оставим как у вас
            var prompt = "Создай пожалуйста изображение " + message;

            foreach (var apiKey in settingsProvider.ApiKeys)
            {
                var model = modelConfig.GetModel(NeuralEntities.Image);
                var url = $"{GeminiSettingsProvider.BaseApiUrl}/{model}:generateContent";

                var jsonData = GenerateJsonPayload(prompt, base64Image);

                using var req = new HttpRequestMessage(HttpMethod.Post, url);
                req.Headers.Add("x-goog-api-key", apiKey);
                req.Content = new StringContent(jsonData, Encoding.UTF8, "application/json");

                using var resp = await httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                {
                    var errorBody = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    Log.Warning("Gemini API returned {Status} for key tail=...{Tail}: {Body}",
                        (int) resp.StatusCode, apiKey[^4..], errorBody.Length <= 500 ? errorBody : errorBody[..500] + "...");
                    continue;
                }

                using var stream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);
                var (text, image) = await ProcessImageStreamAsync(stream).ConfigureAwait(false);

                if (!string.IsNullOrEmpty(image))
                    return (text, image);
            }

            return (null, null);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при получении ответа от Gemini.");
            return (null, null);
        }
    }

    /// <summary>
    /// Обработка ответа: вытащить текст и первую картинку (base64) из parts.
    /// </summary>
    public async Task<(string? Text, string? Base64Image)> ProcessImageStreamAsync(Stream? imageStream)
    {
        if (imageStream == null) return (null, null);

        string? textResult = null;
        string? base64ImageResult = null;

        try
        {
            using var reader = new StreamReader(imageStream, Encoding.UTF8);
            var jsonResponseString = await reader.ReadToEndAsync().ConfigureAwait(false);

            var jsonResponse = JObject.Parse(jsonResponseString);
            var parts = jsonResponse["candidates"]?[0]?["content"]?["parts"] as JArray;

            if (parts != null)
                foreach (var part in parts)
                {
                    // Текстовая часть
                    var textToken = part["text"];
                    if (textToken != null)
                        textResult = textToken.ToString();

                    // Картинка приходит в part.inline_data.data
                    if (part["inline_data"] is JObject inlineData)
                    {
                        var mimeType = inlineData["mime_type"]?.ToString();
                        var data = inlineData["data"]?.ToString();
                        if (!string.IsNullOrEmpty(data) && mimeType != null && mimeType.StartsWith("image/"))
                        {
                            base64ImageResult = data;
                            // Берём первую валидную картинку и заканчиваем
                            break;
                        }
                    }
                }

            return (textResult, base64ImageResult);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при обработке потока.");
            return (null, null);
        }
        finally
        {
            await imageStream.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Собираем JSON для REST API Gemini 2.5 Flash Image Preview:
    /// contents -> parts[], где текст и (опционально) inline_data c mime_type и data (base64)
    /// </summary>
    private static string GenerateJsonPayload(string message, string? imageBase64 = null)
    {
        var parts = new JArray
        {
            new JObject {["text"] = message}
        };

        if (!string.IsNullOrEmpty(imageBase64))
            parts.Add(new JObject
            {
                ["inline_data"] = new JObject
                {
                    ["mime_type"] = "image/png",
                    ["data"] = imageBase64
                }
            });

        var payload = new JObject
        {
            ["contents"] = new JArray
            {
                new JObject
                {
                    ["parts"] = parts
                }
            }
        };

        return payload.ToString();
    }
}