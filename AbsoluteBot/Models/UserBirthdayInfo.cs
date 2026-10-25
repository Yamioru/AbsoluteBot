namespace AbsoluteBot.Models;

#pragma warning disable IDE0028
/// <summary>
///     Представляет информацию о дне рождения пользователя.
/// </summary>
public class UserBirthdayInfo
{
    /// <summary>
    ///     Дата рождения пользователя.
    /// </summary>
    public DateTime DateOfBirth { get; set; }
    /// <summary>
    ///     Словарь платформ и их включены ли уведомлений на них.
    /// </summary>
    public Dictionary<string, bool> NotifyOnPlatforms { get; set; } = new();
    /// <summary>
    ///     Словарь дат последних поздравлений для каждой платформы.
    /// </summary>
    public Dictionary<string, DateTime?> LastCongratulationDate { get; set; } = new();
    /// <summary>
    ///     Список псевдонимов пользователя.
    /// </summary>
    public List<string> Nicknames { get; set; } = new();
    /// <summary>
    ///     Id пользователя по платформам (VkPlayLive, Twitch, Telegram, Discord).
    /// </summary>
    public Dictionary<string, string> PlatformIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    ///     Имя пользователя.
    /// </summary>
    public required string UserName { get; set; }
}