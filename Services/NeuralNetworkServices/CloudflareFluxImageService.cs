using System.Net.Http.Headers;
using System.Text.Json;
using AbsoluteBot.Services.UtilityServices;
using Serilog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace AbsoluteBot.Services.NeuralNetworkServices;

/// <summary>
///     Генерация и правка изображений через Cloudflare Workers AI (FLUX.2 klein-4b).
///     Используется живой командой <c>!нарисуй</c>.
/// </summary>
public class CloudflareFluxImageService(ConfigService configService, HttpClient httpClient) : IAsyncInitializable
{
    public const string ModelId = "@cf/black-forest-labs/flux-2-klein-4b";
    private const int OutputSize = 1024;
    private const int MaxInputImageSize = 512;

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
    ///     Генерирует изображение по промпту или правит переданную картинку.
    /// </summary>
    /// <param name="prompt">Текстовый запрос.</param>
    /// <param name="base64Image">Опционально: входное изображение в base64 (без префикса data:).</param>
    /// <returns>Картинка в base64 или null.</returns>
    public virtual async Task<string?> GenerateOrEditAsync(string prompt, string? base64Image = null)
    {
        try
        {
            if (string.IsNullOrEmpty(_accountId) || string.IsNullOrEmpty(_apiKey))
            {
                Log.Warning("CloudFlare API ключ или Account ID не установлены.");
                return null;
            }

            var url = $"https://api.cloudflare.com/client/v4/accounts/{_accountId}/ai/run/{ModelId}";
            var content = BuildMultipartContent(prompt, base64Image);
            if (content == null)
                return null;

            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = content
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            using var response = await httpClient.SendAsync(request).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                Log.Error("CloudFlare FLUX.2 вернул ошибку: {StatusCode}. {Body}",
                    response.StatusCode, errorBody.Length <= 500 ? errorBody : errorBody[..500] + "...");
                return null;
            }

            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (mediaType != null && mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                return Convert.ToBase64String(bytes);
            }

            var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return ParseImageBase64(responseContent);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при генерации или правке изображения через Cloudflare FLUX.2.");
            return null;
        }
    }

    private static MultipartFormDataContent? BuildMultipartContent(string prompt, string? base64Image)
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(prompt), "prompt");
        content.Add(new StringContent(OutputSize.ToString()), "width");
        content.Add(new StringContent(OutputSize.ToString()), "height");

        if (string.IsNullOrEmpty(base64Image))
            return content;

        var pngBytes = PrepareInputImage(base64Image);
        if (pngBytes == null)
        {
            content.Dispose();
            return null;
        }

        var imageContent = new ByteArrayContent(pngBytes);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(imageContent, "input_image_0", "input.png");
        return content;
    }

    private static byte[]? PrepareInputImage(string base64Image)
    {
        try
        {
            var bytes = Convert.FromBase64String(base64Image);
            using var image = Image.Load(bytes);
            if (image.Width > MaxInputImageSize || image.Height > MaxInputImageSize)
                image.Mutate(ctx => ctx.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(MaxInputImageSize, MaxInputImageSize)
                }));

            using var stream = new MemoryStream();
            image.SaveAsPng(stream);
            return stream.ToArray();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Не удалось подготовить входное изображение для FLUX.2.");
            return null;
        }
    }

    private static string? ParseImageBase64(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.TryGetProperty("result", out var result))
            {
                if (result.ValueKind == JsonValueKind.String)
                    return result.GetString();
                if (result.TryGetProperty("image", out var nestedImage))
                    return nestedImage.GetString();
            }

            if (root.TryGetProperty("image", out var image))
                return image.GetString();

            Log.Error("Ответ Cloudflare FLUX.2 не содержит изображения.");
            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Не удалось разобрать ответ Cloudflare FLUX.2.");
            return null;
        }
    }
}
