using Newtonsoft.Json;
using AbsoluteBot.Models;
using Serilog;

namespace AbsoluteBot.Services.MediaServices;

/// <summary>
///     Сервис для получения изображения кота с API Cataas.
/// </summary>
public class CatImageService(HttpClient httpClient)
{
    private const string CatApiUrl = "https://cataas.com/cat?json=true";
    private const string DefaultCatImageUrl = "https://i.ytimg.com/vi/KoVjqdETurw/maxresdefault.jpg";

    /// <summary>
    ///     Получает случайное изображение кота с API Cataas.
    /// </summary>
    /// <returns>URL изображения кота или URL изображения кота-заглушки в случае ошибки.</returns>
    public virtual async Task<string> GetCatImageAsync()
    {
        try
        {
            var response = await httpClient.GetAsync(CatApiUrl).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode) return DefaultCatImageUrl;
            var jsonResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var catData = JsonConvert.DeserializeObject<CatApiResponse>(jsonResponse);

            return catData != null
                ? $"https://cataas.com/cat/{catData.Id}"
                : DefaultCatImageUrl;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при получении картинки котика.");
            return DefaultCatImageUrl;
        }
    }
}