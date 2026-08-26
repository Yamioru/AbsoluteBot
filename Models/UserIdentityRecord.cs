namespace AbsoluteBot.Models;

/// <summary>
///     Связка никнеймов и id одной личности на платформах (VK Live, Twitch, Telegram, Discord).
/// </summary>
public class UserIdentityRecord
{
    public List<string> Nicknames { get; set; } = new();
    public Dictionary<string, string> Ids { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
