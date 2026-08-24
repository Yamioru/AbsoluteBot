namespace AbsoluteBot.Models;

/// <summary>
///     Каталог ачивок из <c>achievements.json</c>.
/// </summary>
public class AchievementCatalogDocument
{
    public List<AchievementDefinition> Achievements { get; set; } = [];
}

/// <summary>
///     Описание одной ачивки для классификации и учёта.
/// </summary>
public class AchievementDefinition
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public int Goal { get; set; }
    public required string Criteria { get; set; }
}

/// <summary>
///     Прогресс всех чаттеров по ачивкам.
/// </summary>
public class AchievementProgressDocument
{
    public List<AchievementUserProgress> Users { get; set; } = [];
}

/// <summary>
///     Прогресс одного ника.
/// </summary>
public class AchievementUserProgress
{
    public required string Nickname { get; set; }
    public List<AchievementEntryProgress> Achievements { get; set; } = [];
}

/// <summary>
///     Баллы по одной ачивке у ника.
/// </summary>
public class AchievementEntryProgress
{
    public required string Id { get; set; }
    public int Points { get; set; }
    public int Goal { get; set; }
    public bool Unlocked { get; set; }
    public string? LastMatch { get; set; }
    public DateTimeOffset? LastAtUtc { get; set; }
}

/// <summary>
///     Ответ классификатора: список id ачивок.
/// </summary>
public class AchievementClassificationResponse
{
    public List<string> Ids { get; set; } = [];
}
