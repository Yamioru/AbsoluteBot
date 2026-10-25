using System.Text.Json.Serialization;
using AbsoluteBot.Helpers;

namespace AbsoluteBot.Models;

/// <summary>
///     Представляет ответы на Ответы Mail.ru.
/// </summary>
public class MailRuAnswer
{
    /// <summary>
    ///     Лучший ответ на вопрос.
    /// </summary>
    [JsonPropertyName("best")]
    public Answer? BestAnswer { get; set; }
    /// <summary>
    ///     Список всех ответов на вопрос.
    /// </summary>
    [JsonPropertyName("answers")]
    public required List<Answer> Answers { get; set; }
}

/// <summary>
///     Представляет отдельный ответ с количеством лайков и текстом ответа.
/// </summary>
public class Answer
{
    /// <summary>
    ///     Количество лайков, полученных ответом.
    /// </summary>
    [JsonPropertyName("totalmarks")]
    [JsonConverter(typeof(MailRuLikesConverter))]
    public int Likes { get; set; }
    /// <summary>
    ///     Текст ответа.
    /// </summary>
    [JsonPropertyName("atext")]
    public required string Text { get; set; }
}