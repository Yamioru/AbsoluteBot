using Newtonsoft.Json;

namespace AbsoluteBot.Models;

/// <summary>
///     Класс, представляющий ответ от API с изображением кота.
/// </summary>
public class CatApiResponse
{
    /// <summary>
    ///     Уникальный идентификатор изображения кота.
    /// </summary>
    [JsonProperty("_id")]
    public required string Id { get; set; }
    /// <summary>
    ///     Теги, связанные с изображением кота.
    /// </summary>
    [JsonProperty("tags")]
    public required string[] Tags { get; set; }
}