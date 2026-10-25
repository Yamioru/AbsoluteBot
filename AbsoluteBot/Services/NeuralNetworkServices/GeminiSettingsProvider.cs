using System.Text;
using AbsoluteBot.Services.UtilityServices;
using Newtonsoft.Json.Linq;
using Serilog;

namespace AbsoluteBot.Services.NeuralNetworkServices;

/// <summary>
///     Предоставляет настройки и методы для взаимодействия с моделью Gemini, включая доступ к API ключам.
///     Использует отдельный HttpClient, чтобы чужие DefaultRequestHeaders не ломали запросы.
/// </summary>
public class GeminiSettingsProvider(ConfigService configService, HttpClient httpClient) : IAsyncInitializable
{
    public const string BaseApiUrl = "https://generativelanguage.googleapis.com/v1beta/models";
    public List<string>? ApiKeys;

    public async Task InitializeAsync()
    {
        ApiKeys = await configService.GetConfigValueAsync<List<string>>("GeminiApiKeys").ConfigureAwait(false);
        if (ApiKeys == null || ApiKeys.Count < 1)
            Log.Warning("Не удалось загрузить api ключи для gemini.");
    }

    /// <summary>
    ///     Собирает URL generateContent для модели и ключа.
    /// </summary>
    public static string BuildGenerateContentUrl(string model, string apiKey) =>
        $"{BaseApiUrl}/{model}:generateContent?key={apiKey}";

    /// <summary>
    ///     Отправка HTTP-запроса к модели и получение потока ответа.
    /// </summary>
    public async Task<Stream?> FetchImageModelResponseStreamAsync(string jsonData, string url)
    {
        var content = new StringContent(jsonData, Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, url) {Content = content};

        try
        {
            var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                Log.Warning("Gemini image API вернул {Status}: {Body}", (int) response.StatusCode, Truncate(errorBody));
                return null;
            }

            return await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при получении потокового ответа от Gemini.");
            return null;
        }
    }

    /// <summary>
    ///     Отправка HTTP-запроса к модели и получение текста ответа.
    /// </summary>
    public async Task<string?> FetchModelResponseAsync(string jsonData, string url)
    {
        try
        {
            var content = new StringContent(jsonData, Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Post, url) {Content = content};
            var response = await httpClient.SendAsync(request).ConfigureAwait(false);
            var result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                Log.Warning("Gemini API вернул {Status}: {Body}", (int) response.StatusCode, Truncate(result));
                return null;
            }

            return ParseResponse(result);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при запросе к Gemini.");
            return null;
        }
    }

    /// <summary>
    ///     Извлечение текста ответа из JSON-ответа модели.
    /// </summary>
    private static string? ParseResponse(string result)
    {
        var jsonResponse = JObject.Parse(result);
        if (jsonResponse["error"] != null)
        {
            Log.Warning("Gemini API error: {Error}", jsonResponse["error"]!.ToString());
            return null;
        }

        var texts = jsonResponse["candidates"]?
            .SelectMany(candidate => candidate["content"]?["parts"] ?? new JArray())
            .Select(part => part?["text"]?.ToString())
            .Where(text => !string.IsNullOrEmpty(text));

        var joined = texts != null ? string.Join(" ", texts) : null;
        if (!string.IsNullOrWhiteSpace(joined))
            return joined;

        var finishReason = jsonResponse["candidates"]?[0]?["finishReason"]?.ToString();
        var promptFeedback = jsonResponse["promptFeedback"]?.ToString();
        if (!string.IsNullOrEmpty(finishReason) || !string.IsNullOrEmpty(promptFeedback))
            Log.Warning("Gemini вернул пустой текст. finishReason={FinishReason}, promptFeedback={PromptFeedback}",
                finishReason, promptFeedback);

        return null;
    }

    private static string Truncate(string value) =>
        value.Length <= 500 ? value : value[..500] + "...";
}
