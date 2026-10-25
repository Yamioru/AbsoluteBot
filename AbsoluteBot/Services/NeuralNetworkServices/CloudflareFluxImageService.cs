using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
    private const int OutputMin = 256;
    private const int OutputMax = 1024;
    /// <summary>Документация FLUX.2: вход меньше 512×512, не меньше-или-равно.</summary>
    private const int MaxInputImageSize = 510;
    private const double EditGuidance = 2.5;

    private static readonly Regex UrlPattern = new(@"https?://\S+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

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

            byte[]? inputPng = null;
            var outputWidth = OutputSize;
            var outputHeight = OutputSize;
            var isEdit = !string.IsNullOrEmpty(base64Image);
            if (isEdit)
            {
                var prepared = PrepareInputImage(base64Image!);
                if (prepared == null)
                    return null;
                inputPng = prepared.Value.Png;
                (outputWidth, outputHeight) = ComputeOutputSize(prepared.Value.Width, prepared.Value.Height);
            }

            var cleanedPrompt = isEdit ? UrlPattern.Replace(prompt, string.Empty).Trim() : prompt;
            if (string.IsNullOrWhiteSpace(cleanedPrompt))
                cleanedPrompt = prompt;

            var requestPrompt = isEdit ? WrapEditPrompt(cleanedPrompt) : cleanedPrompt;
            var image = await PostFluxAsync(requestPrompt, inputPng, outputWidth, outputHeight, isEdit).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(image.Base64))
                return image.Base64;

            if (!image.Flagged)
                return null;

            Log.Warning("Cloudflare FLUX.2 отфильтровал результат (3030), повтор с более явным промптом.");
            var retryPrompt = isEdit ? WrapEditPromptSafe(cleanedPrompt) : WrapGeneratePromptSafe(cleanedPrompt);
            var retry = await PostFluxAsync(retryPrompt, inputPng, outputWidth, outputHeight, isEdit).ConfigureAwait(false);
            return retry.Base64;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при генерации или правке изображения через Cloudflare FLUX.2.");
            return null;
        }
    }

    internal static string WrapEditPrompt(string prompt) =>
        "Edit image 0. Apply only this change and keep everything else the same, including people, clothing, faces, and composition: " + prompt;

    internal static string WrapEditPromptSafe(string prompt) =>
        "Family-friendly photo edit of image 0. Keep the result non-explicit and anatomically normal. Change: " + prompt;

    internal static string WrapGeneratePromptSafe(string prompt) =>
        "A detailed family-friendly illustration of: " + prompt;

    internal static (int Width, int Height) ComputeOutputSize(int sourceWidth, int sourceHeight)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0)
            return (OutputSize, OutputSize);

        var scale = (double) OutputMax / Math.Max(sourceWidth, sourceHeight);
        var width = AlignDimension((int) Math.Round(sourceWidth * scale));
        var height = AlignDimension((int) Math.Round(sourceHeight * scale));
        return (width, height);
    }

    internal static bool IsFlaggedError(string? errorBody)
    {
        if (string.IsNullOrEmpty(errorBody))
            return false;
        if (errorBody.Contains("\"code\":3030", StringComparison.Ordinal) ||
            errorBody.Contains("flagged", StringComparison.OrdinalIgnoreCase) ||
            errorBody.Contains("NSFW", StringComparison.OrdinalIgnoreCase))
            return true;

        try
        {
            using var document = JsonDocument.Parse(errorBody);
            if (!document.RootElement.TryGetProperty("errors", out var errors) || errors.ValueKind != JsonValueKind.Array)
                return false;
            foreach (var error in errors.EnumerateArray())
                if (error.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.Number && code.GetInt32() == 3030)
                    return true;
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private async Task<(string? Base64, bool Flagged)> PostFluxAsync(
        string prompt, byte[]? inputPng, int width, int height, bool isEdit)
    {
        var url = $"https://api.cloudflare.com/client/v4/accounts/{_accountId}/ai/run/{ModelId}";
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = BuildMultipartContent(prompt, inputPng, width, height, isEdit)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        using var response = await httpClient.SendAsync(request).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var flagged = IsFlaggedError(errorBody);
            if (flagged)
                Log.Warning("CloudFlare FLUX.2 отфильтровал запрос: {StatusCode}. {Body}",
                    response.StatusCode, Truncate(errorBody));
            else
                Log.Error("CloudFlare FLUX.2 вернул ошибку: {StatusCode}. {Body}",
                    response.StatusCode, Truncate(errorBody));
            return (null, flagged);
        }

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType != null && mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            return (Convert.ToBase64String(bytes), false);
        }

        var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return (ParseImageBase64(responseContent), false);
    }

    private static MultipartFormDataContent BuildMultipartContent(
        string prompt, byte[]? inputPng, int width, int height, bool isEdit)
    {
        var content = new MultipartFormDataContent();
        content.Add(TextPart(prompt), "prompt");
        content.Add(TextPart(width.ToString()), "width");
        content.Add(TextPart(height.ToString()), "height");
        if (isEdit)
            content.Add(TextPart(EditGuidance.ToString(System.Globalization.CultureInfo.InvariantCulture)), "guidance");

        if (inputPng == null)
            return content;

        var imageContent = new ByteArrayContent(inputPng);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(imageContent, "input_image_0", "input.png");
        return content;
    }

    private static StringContent TextPart(string value)
    {
        var part = new StringContent(value, Encoding.UTF8);
        part.Headers.ContentType = null;
        return part;
    }

    private static (byte[] Png, int Width, int Height)? PrepareInputImage(string base64Image)
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
            return (stream.ToArray(), image.Width, image.Height);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Не удалось подготовить входное изображение для FLUX.2.");
            return null;
        }
    }

    private static int AlignDimension(int value)
    {
        var aligned = Math.Max(OutputMin, value / 8 * 8);
        return Math.Clamp(aligned, OutputMin, 1920);
    }

    private static string Truncate(string errorBody) =>
        errorBody.Length <= 500 ? errorBody : errorBody[..500] + "...";

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
