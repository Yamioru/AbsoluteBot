using System.Text;
using AbsoluteBot.Services.UtilityServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace AbsoluteBot.Services;

/// <summary>
///     Сервис для перевода текста с использованием DeepL API.
/// </summary>
public class TranslationService(HttpClient httpClient, ConfigService configService) : IAsyncInitializable
{
    private const string ApiUrl = "https://api-free.deepl.com/v2/translate";
    private const string DefaultFormality = "prefer_less";
    private bool _isConfigured;

    public async Task InitializeAsync()
    {
        var apiKey = await configService.GetConfigValueAsync<string>("DeepLApiKey").ConfigureAwait(false);
        httpClient.DefaultRequestHeaders.Add("Authorization", "DeepL-Auth-Key " + apiKey);
        if (string.IsNullOrEmpty(apiKey))
            Log.Warning("Не удалось загрузить api ключ для deepl.");
        else
            _isConfigured = true;
    }

    /// <summary>
    ///     Выполняет асинхронный перевод текста на указанный язык с использованием DeepL API.
    /// </summary>
    /// <param name="text">Текст для перевода.</param>
    /// <param name="targetLanguage">Целевой язык перевода (например, "EN" для английского).</param>
    /// <returns>
    ///     Возвращает переведенный текст, если перевод успешен, иначе <c>null</c>.
    /// </returns>
    public virtual async Task<string?> TranslateTextAsync(string text, string targetLanguage)
    {
        try
        {
            if (!_isConfigured) return null;
            var requestData = CreateTranslationRequest(text, targetLanguage);
            var response = await SendTranslationRequestAsync(requestData).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode) return null;

            var translatedText = await ParseTranslationResponseAsync(response).ConfigureAwait(false);
            return translatedText;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при переводе.");
            return null;
        }
    }

    /// <summary>
    ///     Создает объект данных для запроса на перевод.
    /// </summary>
    /// <param name="text">Текст, который требуется перевести.</param>
    /// <param name="targetLanguage">Целевой язык перевода (например, "EN").</param>
    /// <returns>Возвращает объект с данными для отправки в запросе на API.</returns>
    private static object CreateTranslationRequest(string text, string targetLanguage)
    {
        return new
        {
            text = new[] {text},
            target_lang = targetLanguage,
            formality = DefaultFormality
        };
    }

    /// <summary>
    ///     Обрабатывает ответ от API DeepL и извлекает переведенный текст.
    /// </summary>
    /// <param name="response">Ответ от API в формате <see cref="HttpResponseMessage" />.</param>
    /// <returns>Возвращает переведенный текст или <c>null</c> в случае неудачи.</returns>
    /// <exception cref="JsonException">Выбрасывается при ошибке парсинга ответа API.</exception>
    private static async Task<string?> ParseTranslationResponseAsync(HttpResponseMessage response)
    {
        var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var jsonResponse = JObject.Parse(responseBody);
        return jsonResponse["translations"]?[0]?["text"]?.ToString();
    }

    /// <summary>
    ///     Отправляет запрос на перевод текста к API DeepL.
    /// </summary>
    /// <param name="requestData">Данные для запроса в формате JSON.</param>
    /// <returns>Возвращает ответ сервера API в формате <see cref="HttpResponseMessage" />.</returns>
    /// <exception cref="HttpRequestException">Выбрасывается при ошибке HTTP-запроса.</exception>
    private async Task<HttpResponseMessage> SendTranslationRequestAsync(object requestData)
    {
        var jsonRequestData = JsonConvert.SerializeObject(requestData);
        using var content = new StringContent(jsonRequestData, Encoding.UTF8, "application/json");
        return await httpClient.PostAsync(ApiUrl, content).ConfigureAwait(false);
    }
}