using Serilog;

namespace AbsoluteBot.Services.UtilityServices;

/// <summary>
///     Сервис для сокращения URL через API сервиса TinyURL.
/// </summary>
public class UrlShortenerService(HttpClient httpClient)
{
    private const string TinyUrlApiEndpoint = "https://tinyurl.com/api-create.php?url=";

    /// <summary>
    ///     Сокращает URL, используя API TinyURL.
    /// </summary>
    /// <param name="url">Полный URL, который нужно сократить.</param>
    /// <returns>Сокращённый URL или <c>null</c> в случае ошибки.</returns>
    public async Task<string?> ShrinkUrlAsync(string url)
    {
        try
        {
            var requestUrl = $"{TinyUrlApiEndpoint}{Uri.EscapeDataString(url)}";
            var response = await httpClient.GetStringAsync(requestUrl).ConfigureAwait(false);
            return response;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при сокращении ссылки.");
            return null;
        }
    }
}