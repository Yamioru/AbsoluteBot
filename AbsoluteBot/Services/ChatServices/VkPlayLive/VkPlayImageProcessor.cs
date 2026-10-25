using AbsoluteBot.Chat.Context;
using Serilog;

namespace AbsoluteBot.Services.ChatServices.VkPlayLive;

/// <summary>
///     Класс для обработки ссылок на изображения в сообщениях VK Play и конвертации их в формат Base64.
/// </summary>
public class VkPlayImageProcessor(HttpClient httpClient) : BaseImageProcessor(httpClient)
{
    /// <summary>
    ///     Проверяет ссылки на изображения в текущем и родительском сообщении и возвращает содержимое изображения в формате
    ///     Base64.
    /// </summary>
    /// <param name="context">Контекст чата, содержащий ссылки на изображения в текущем и родительском сообщении.</param>
    /// <returns>Строка в формате Base64 или <c>null</c>, если изображение не найдено.</returns>
    public async Task<string?> GetBase64ImageFromUrlsAsync(VkPlayChatContext context)
    {
        try
        {
            var base64Image = await CheckUrlsForImage(context.Urls).ConfigureAwait(false);
            return base64Image ?? null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при проверке ссылок на изображение в VK Play.");
            return null;
        }
    }

    /// <summary>
    ///     Проверяет список URL-адресов на наличие изображений и загружает их.
    /// </summary>
    /// <param name="urls">Список URL-адресов.</param>
    /// <returns>Строка в формате Base64 или <c>null</c>, если изображение не найдено.</returns>
    private async Task<string?> CheckUrlsForImage(List<string> urls)
    {
        foreach (var url in urls.Where(IsImageUrl))
        {
            var base64Image = await DownloadFileFromUrlAsync(url).ConfigureAwait(false);
            if (base64Image != null) return base64Image;
        }

        return null;
    }
}