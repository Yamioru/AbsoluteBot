namespace AbsoluteBot.Models;

/// <summary>
///     Файл статистики сообщений чаттеров за один стрим.
/// </summary>
public class StreamChatterStatsDocument
{
    public int StreamNumber { get; set; }
    public List<StreamChatterCount> Chatters { get; set; } = [];
}

/// <summary>
///     Количество сообщений одного чаттера за стрим.
/// </summary>
public class StreamChatterCount
{
    public required string Nickname { get; set; }
    public int Count { get; set; }
}
