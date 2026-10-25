using AbsoluteBot.Services.ChatServices.VkPlayLive;

namespace AbsoluteBotTests.Handlers;

[Trait("Category", "Unit")]
[Trait("Area", "Handlers")]
public class VkPlayConnectionManagerTests
{
    [Fact]
    public void BuildCookieHeader_ReplacesAuthPlaceholder()
    {
        var header = VkPlayConnectionManager.BuildCookieHeader(
            "theme=dark; {auth}; extra=1",
            "auth=token");

        Assert.Equal("theme=dark; auth=token; extra=1", header);
        Assert.DoesNotContain("{auth}", header);
    }

    [Fact]
    public void BuildCookieHeader_AppendsAuth_WhenNoPlaceholder()
    {
        var header = VkPlayConnectionManager.BuildCookieHeader("theme=dark;", "auth=token");

        Assert.Equal("theme=dark; auth=token", header);
    }

    [Fact]
    public void SplitWebSocketPayload_SplitsNewLineSeparatedJson()
    {
        const string payload =
            """{"id":1,"connect":{"client":"abc"}}""" + "\n" + """{"id":2,"subscribe":{}}""";

        var parts = VkPlayMessageReceiver.SplitWebSocketPayload(payload);

        Assert.Equal(2, parts.Count);
        Assert.Contains("\"id\":1", parts[0]);
        Assert.Contains("\"id\":2", parts[1]);
    }

    [Fact]
    public void ChatSubscriptions_UsesChannelChat()
    {
        var subs = VkPlayCentrifugoProtocol.ChatSubscriptions("10898237");

        Assert.Single(subs);
        Assert.Equal((2, "channel-chat:10898237"), subs[0]);
    }

    [Fact]
    public void DescribeFrame_MarksCentrifugoError()
    {
        var frame = VkPlayCentrifugoProtocol.DescribeFrame("""{"id":2,"error":{"code":105,"message":"permission denied"}}""");

        Assert.Equal("error", frame.Kind);
        Assert.True(frame.LogAtInformation);
        Assert.Contains("105", frame.Detail);
    }

    [Fact]
    public void DescribeFrame_MessageV8IsChatPush()
    {
        var frame = VkPlayCentrifugoProtocol.DescribeFrame(
            """{"push":{"channel":"channel-chat:10898237","pub":{"data":{"type":"message_v8"}}}}""");

        Assert.Equal("push", frame.Kind);
        Assert.Contains("message_v8", frame.Detail);
        Assert.False(frame.LogAtInformation);
    }

    [Fact]
    public void DescribeFrame_ConnectAck()
    {
        var frame = VkPlayCentrifugoProtocol.DescribeFrame(
            """{"id":1,"connect":{"client":"abc","ping":25,"pong":true}}""");

        Assert.Equal("connect-ack", frame.Kind);
        Assert.True(frame.LogAtInformation);
    }

    [Fact]
    public void SplitWebSocketPayload_SingleJson_Unchanged()
    {
        const string payload = """{"push":{"pub":{}}}""";

        var parts = VkPlayMessageReceiver.SplitWebSocketPayload(payload);

        Assert.Single(parts);
        Assert.Equal(payload, parts[0]);
    }
}
