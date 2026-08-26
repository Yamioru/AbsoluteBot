using System.Text.Json;

namespace AbsoluteBot.Services.ChatServices.VkPlayLive;

/// <summary>
///     Кадры Centrifugo v2 для VK Live: connect/subscribe и имена каналов чата.
/// </summary>
internal static class VkPlayCentrifugoProtocol
{
    public const int ConnectCommandId = 1;
    public const string PublicChatPrefix = "public-chat:";
    public const string LegacyChatPrefix = "channel-chat:";

    public static string ConnectPayload(string token) =>
        "{\"connect\":{\"token\":\"" + token + "\",\"name\":\"js\"},\"id\":" + ConnectCommandId + "}";

    public static IReadOnlyList<(int Id, string Channel)> ChatSubscriptions(string channelId)
    {
        return new[]
        {
            (2, PublicChatPrefix + channelId),
            (3, LegacyChatPrefix + channelId)
        };
    }

    public static string SubscribePayload(int id, string channel) =>
        "{\"subscribe\":{\"channel\":\"" + channel + "\"},\"id\":" + id + "}";

    public static bool IsPing(string part) => part == "{}";

    public static VkPlayCentrifugoFrame DescribeFrame(string json)
    {
        if (IsPing(json)) return new VkPlayCentrifugoFrame("ping", "pong", false);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var id = root.TryGetProperty("id", out var idEl) ? idEl.ToString() : null;
            if (root.TryGetProperty("error", out var error))
                return new VkPlayCentrifugoFrame("error", $"id={id} {Truncate(error.ToString())}", true);
            if (root.TryGetProperty("connect", out var connect))
                return new VkPlayCentrifugoFrame("connect-ack", Truncate(connect.ToString()), true);
            if (root.TryGetProperty("subscribe", out var subscribe))
                return new VkPlayCentrifugoFrame("subscribe-ack", $"id={id} {Truncate(subscribe.ToString())}", true);
            if (root.TryGetProperty("push", out var push) && push.ValueKind == JsonValueKind.Object)
            {
                var channel = push.TryGetProperty("channel", out var ch) && ch.ValueKind == JsonValueKind.String
                    ? ch.GetString()
                    : null;
                var type = TryGetPushType(push);
                return new VkPlayCentrifugoFrame("push", $"channel={channel} type={type}", type != "message");
            }

            return new VkPlayCentrifugoFrame("other", Truncate(json), true);
        }
        catch (JsonException)
        {
            return new VkPlayCentrifugoFrame("invalid-json", Truncate(json), true);
        }
    }

    private static string? TryGetPushType(JsonElement push)
    {
        if (!push.TryGetProperty("pub", out var pub) || pub.ValueKind != JsonValueKind.Object) return null;
        if (!pub.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object) return null;
        if (data.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String)
            return type.GetString();
        return null;
    }

    private static string Truncate(string? text, int max = 400)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= max) return text ?? string.Empty;
        return text[..max];
    }
}

internal readonly record struct VkPlayCentrifugoFrame(string Kind, string Detail, bool LogAtInformation);
