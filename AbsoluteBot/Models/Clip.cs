namespace AbsoluteBot.Models;

/// <summary>
/// Класс клипа.
/// </summary>
public class Clip
{
    public int Id { get; set; }
    public required string Url { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
}