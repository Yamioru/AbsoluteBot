namespace AbsoluteBot.Models;

/// <summary>
///     Содержит информацию о прогрессе прохождения игры.
/// </summary>
public class GameProgress
{
    /// <summary>
    ///     Текущее время, затраченное на прохождение игры, в минутах.
    /// </summary>
    public required int CurrentPlaytimeMinutes { get; set; }
    /// <summary>
    ///     Последний зафиксированный целочисленный процент прогресса игры.
    /// </summary>
    public required int LastLoggedPercentage { get; set; }
    /// <summary>
    ///     Полное время, необходимое для прохождения игры, в минутах.
    /// </summary>
    public required int TotalPlaytimeMinutes { get; set; }
    /// <summary>
    ///     Название игры.
    /// </summary>
    public required string GameName { get; set; }
}