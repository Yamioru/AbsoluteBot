using System.Text.Json.Serialization;

namespace AbsoluteBot.Models;

/// <summary>
///     Класс для десериализации ответа от HowLongToBeat API.
/// </summary>
public class HowLongToBeatSearchResponse
{
    /// <summary>
    ///     Лист найденных игр.
    /// </summary>
    [JsonPropertyName("data")]
    public required List<GameData> Data { get; set; }
}

/// <summary>
///     Класс для десериализации данных о конкретной игре из ответа HowLongToBeat API.
/// </summary>
public class GameData
{
    /// <summary>
    ///     Время прохождение игры на 100% в секундах.
    /// </summary>
    [JsonPropertyName("comp_100")]
    public int Comp100 { get; set; }
    /// <summary>
    ///     Время прохождение игры по основному квесту в секундах.
    /// </summary>
    [JsonPropertyName("comp_main")]
    public int CompMain { get; set; }
    /// <summary>
    ///     Время прохождение игры по основному квесту и с некоторыми дополнительными квестами в секундах.
    /// </summary>
    [JsonPropertyName("comp_plus")]
    public int CompPlus { get; set; }
}