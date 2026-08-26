using System.Text.Json.Serialization;

namespace AbsoluteBot.Models;

/// <summary>
///     Оболочка для данных сообщения VkPlay, содержащая информацию о push-событии.
/// </summary>
public class VkPlayMessageEnvelope
{
    [JsonPropertyName("push")] public VkPlayPushData? Push { get; set; }
}

/// <summary>
///     Класс, представляющий блок сообщения с содержимым и типом.
/// </summary>
public class MessageBlock
{
    [JsonPropertyName("content")] public string? Content { get; set; }
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("url")] public string? Url { get; set; }
}

/// <summary>
///     Класс, представляющий данные push-события VkPlay.
/// </summary>
public class VkPlayPushData
{
    [JsonPropertyName("pub")] public VkPlayPublicationData? Publication { get; set; }
}

/// <summary>
///     Класс, содержащий данные публикации VkPlay.
/// </summary>
public class VkPlayPublicationData
{
    [JsonPropertyName("data")] public VkPlayMessageContainer? MessageContainer { get; set; }
}

/// <summary>
///     Контейнер для сообщения, содержащий тип сообщения и его данные.
/// </summary>
public class VkPlayMessageContainer
{
    [JsonPropertyName("type")] public string? MessageType { get; set; }
    [JsonPropertyName("data")] public VkPlayMessage? Message { get; set; }
}

/// <summary>
///     Класс, представляющий сообщение пользователя в VkPlayLive.
/// </summary>
public class VkPlayMessage
{
    [JsonPropertyName("id")] public int MessageId { get; set; }
    [JsonPropertyName("data")] public List<VkPlayMessageContent>? Contents { get; set; }
    [JsonPropertyName("parent")] public VkPlayParentMessage? ParentMessage { get; set; }
    [JsonPropertyName("author")] public VkPlayUser? Author { get; set; }
}

/// <summary>
///     Класс, представляющий пользователя в VkPlayLive.
/// </summary>
public class VkPlayUser
{
    [JsonPropertyName("name")] public string? UserName { get; set; }
    [JsonPropertyName("nick")] public string? Nick { get; set; }
    [JsonPropertyName("displayName")] public string? DisplayName { get; set; }

    public string? ResolveDisplayName() =>
        FirstNonEmpty(UserName, Nick, DisplayName);

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        return null;
    }
}

/// <summary>
///     Класс, представляющий родительское сообщение в VkPlayLive.
/// </summary>
public class VkPlayParentMessage
{
    [JsonPropertyName("data")] public List<VkPlayMessageContent>? Contents { get; set; }
    [JsonPropertyName("author")] public VkPlayUser? ParentAuthor { get; set; }
}

/// <summary>
///     Класс, представляющий содержимое сообщения, включая текст, тип и никнейм.
/// </summary>
public class VkPlayMessageContent
{
    [JsonPropertyName("content")] public string? Content { get; set; }
    [JsonPropertyName("type")] public string? ContentType { get; set; }
    [JsonPropertyName("nick")] public string? Nickname { get; set; }
    [JsonPropertyName("url")] public string? Url { get; set; }
}