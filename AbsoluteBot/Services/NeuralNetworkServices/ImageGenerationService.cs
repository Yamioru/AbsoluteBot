using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AbsoluteBot.Services.UtilityServices;
using Serilog;

namespace AbsoluteBot.Services.NeuralNetworkServices;

public class ImageGenerationService(ConfigService configService, HttpClient httpClient) : IAsyncInitializable
{
    private string? _accountId;
    private string? _apiKey;

    public async Task InitializeAsync()
    {
        _apiKey = await configService.GetConfigValueAsync<string>("CloudFlareApiKey").ConfigureAwait(false);
        if (_apiKey == null)
            Log.Warning("Не удалось загрузить api ключ для CloudFlare.");

        _accountId = await configService.GetConfigValueAsync<string>("CloudFlareAccountId").ConfigureAwait(false);
        if (_accountId == null)
            Log.Warning("Не удалось загрузить account Id для CloudFlare.");
    }

    /// <summary>
    /// Генерирует изображение на основе запроса
    /// </summary>
    /// <param name="prompt">Текст запроса для создания изображения</param>
    /// <returns>base64 строка изображения</returns>
    public string? GenerateImage(string prompt)
    {
        try
        {
            if (string.IsNullOrEmpty(_accountId) || string.IsNullOrEmpty(_apiKey))
            {
                Log.Warning("CloudFlare API ключ или Account ID не установлены.");
                return null;
            }

            var url = $"https://api.cloudflare.com/client/v4/accounts/{_accountId}/ai/run/@cf/black-forest-labs/flux-1-schnell";

            var requestData = new {prompt};
            var jsonContent = JsonSerializer.Serialize(requestData);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = content
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            var response = httpClient.SendAsync(request).Result;

            if (!response.IsSuccessStatusCode)
            {
                Log.Error("CloudFlare API вернул ошибку: {StatusCode}", response.StatusCode);
                return null;
            }

            var responseContent = response.Content.ReadAsStringAsync().Result;

            using var document = JsonDocument.Parse(responseContent);
            var root = document.RootElement;

            if (root.TryGetProperty("result", out var result) &&
                result.TryGetProperty("image", out var imageElement))
                return imageElement.GetString();

            Log.Error("Ответ не содержит 'result.image'.");
            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при генерации изображения.");
            return null;
        }
    }
}