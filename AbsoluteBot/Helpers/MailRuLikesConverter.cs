using System.Text.Json;
using System.Text.Json.Serialization;

namespace AbsoluteBot.Helpers;

/// <summary>
///     Конвертер для преобразования значений лайков ответов mail ru из JSON в целочисленный формат.
/// </summary>
public class MailRuLikesConverter : JsonConverter<int>
{
    /// <summary>
    ///     Преобразует значение из JSON в целочисленный формат.
    /// </summary>
    /// <param name="reader">Читатель JSON-данных.</param>
    /// <param name="typeToConvert">Тип данных, который нужно преобразовать.</param>
    /// <param name="options">Опции сериализации JSON.</param>
    /// <returns>Целочисленное значение лайков.</returns>
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Number => reader.GetInt32(),
            JsonTokenType.String when int.TryParse(reader.GetString(), out var result) => result,
            _ => 0
        };
    }

    /// <summary>
    ///     Записывает целочисленное значение лайков в JSON.
    /// </summary>
    /// <param name="writer">Записывающий объект JSON.</param>
    /// <param name="value">Значение лайков.</param>
    /// <param name="options">Опции сериализации JSON.</param>
    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }
}