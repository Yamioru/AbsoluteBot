using System.Net;
using System.Text;
using AbsoluteBot.Services.ChatServices.VkPlayLive;
using AbsoluteBot.Services.UtilityServices;
using AbsoluteBotTests.Infrastructure;
using Moq;

namespace AbsoluteBotTests;

[Trait("Category", "Unit")]
[Trait("Area", "Handlers")]
public class VkPlayMessageSenderTests
{
    private static (VkPlayMessageSender Sender, MockHttpMessageHandler Handler) CreateSender(
        string token = "test-token",
        string channel = "test-channel")
    {
        var config = new Mock<ConfigService>();
        config.Setup(c => c.GetConfigValueAsync<string>("VkPlayAuthSendToken")).ReturnsAsync(token);
        config.Setup(c => c.GetConfigValueAsync<string>("VkPlayChannelName")).ReturnsAsync(channel);

        var handler = new MockHttpMessageHandler();
        var httpClient = new HttpClient(handler, disposeHandler: false);
        return (new VkPlayMessageSender(config.Object, httpClient), handler);
    }

    [Fact]
    public void SerializeMessageForTests_TextOnly_ProducesTextBlock()
    {
        var json = VkPlayMessageSender.SerializeMessageForTests("Hello world");

        Assert.Contains("\"type\":\"text\"", json);
        Assert.Contains("Hello world", json);
        Assert.DoesNotContain("\"type\":\"link\"", json);
    }

    [Fact]
    public void SerializeMessageForTests_UrlOnly_ProducesLinkBlock()
    {
        var json = VkPlayMessageSender.SerializeMessageForTests("https://example.com");

        Assert.Contains("\"type\":\"link\"", json);
        Assert.Contains("https://example.com", json);
    }

    [Fact]
    public void SerializeMessageForTests_TextAndUrl_ProducesBothBlocks()
    {
        var json = VkPlayMessageSender.SerializeMessageForTests("Check this: https://example.com end");

        Assert.Contains("\"type\":\"text\"", json);
        Assert.Contains("\"type\":\"link\"", json);
        Assert.Contains("Check this: ", json);
        Assert.Contains(" end", json);
    }

    [Fact]
    public async Task PostMessageAsync_WithMockedHttp200_SendsAuthorizedPost()
    {
        var (sender, handler) = CreateSender();
        handler.EnqueueResponse(HttpStatusCode.OK, "{}");
        await sender.InitializeAsync();

        await sender.PostMessageAsync("Test message");

        Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal("https://api.live.vkplay.ru/v1/blog/test-channel/public_video_stream/chat", handler.LastRequestUri);
        Assert.Equal("Bearer test-token", handler.LastAuthorization);

        var body = handler.LastBody!;
        Assert.StartsWith("data=", body);
        Assert.Contains("Test message", Uri.UnescapeDataString(body));
    }

    [Fact]
    public async Task PostMessageAsync_WithReplyId_IncludesReplyParameter()
    {
        var (sender, handler) = CreateSender();
        handler.EnqueueResponse(HttpStatusCode.OK, "{}");
        await sender.InitializeAsync();

        await sender.PostMessageAsync("reply", messageId: 42);

        var body = handler.LastBody!;
        Assert.Contains("reply_to_id=42", body);
    }

    [Fact]
    public async Task PostMessageAsync_WithoutInit_DoesNotSend()
    {
        var (sender, handler) = CreateSender();
        handler.EnqueueResponse(HttpStatusCode.OK, "{}");

        await sender.PostMessageAsync("should not send");

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task PostMessageAsync_Unauthorized_RefreshesAndRetriesWithNewBearer()
    {
        var (sender, sendHandler, auth, authHandler) = CreateSenderWithAuth();
        using (auth)
        {
            await sender.InitializeAsync();
            sendHandler.EnqueueResponse(HttpStatusCode.Unauthorized, "{}");
            authHandler.EnqueueResponse(HttpStatusCode.OK,
                "{\"access_token\":\"new-send\",\"refresh_token\":\"new-refresh\",\"expires_in\":2592000}");
            sendHandler.EnqueueResponse(HttpStatusCode.OK, "{}");

            await sender.PostMessageAsync("hello after refresh");

            Assert.Equal(2, sendHandler.Requests.Count);
            Assert.Equal("Bearer new-send", sendHandler.LastAuthorization);
            Assert.Equal(0, auth.NotifyCount);
        }
    }

    [Fact]
    public async Task PostMessageAsync_Unauthorized_RefreshFails_DoesNotThrow()
    {
        var (sender, sendHandler, auth, authHandler) = CreateSenderWithAuth();
        using (auth)
        {
            await sender.InitializeAsync();
            sendHandler.EnqueueResponse(HttpStatusCode.Unauthorized, "{}");
            authHandler.EnqueueResponse(HttpStatusCode.Unauthorized, "{\"error\":\"invalid_grant\"}");

            var exception = await Record.ExceptionAsync(() => sender.PostMessageAsync("will fail"));

            Assert.Null(exception);
            Assert.Single(sendHandler.Requests);
            Assert.Equal(1, auth.NotifyCount);
        }
    }

    private static (VkPlayMessageSender Sender, MockHttpMessageHandler SendHandler, VkPlayAuthService Auth,
        MockHttpMessageHandler AuthHandler) CreateSenderWithAuth()
    {
        var future = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 3_600_000;
        var tokens = new VkPlayAuthTokens("old-send", "refresh-1", future, "client-1");
        var saved = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["VkPlayAuthSendToken"] = "old-send",
            ["VkPlayChannelName"] = "test-channel",
            [VkPlayAuthService.AuthTokenConfigKey] = VkPlayAuthService.BuildAuthCookie(tokens),
            [VkPlayAuthService.ClientIdConfigKey] = "client-1"
        };

        var config = new Mock<ConfigService>();
        config.Setup(c => c.GetConfigValueAsync<string>(It.IsAny<string>()))
            .Returns((string key) => Task.FromResult(saved.GetValueOrDefault(key)));
        config.Setup(c => c.SetConfigValueAsync(It.IsAny<string>(), It.IsAny<object>()))
            .Callback<string, object>((key, value) => saved[key] = value.ToString() ?? string.Empty)
            .ReturnsAsync(true);

        var authHandler = new MockHttpMessageHandler();
        var auth = new VkPlayAuthService(config.Object, new HttpClient(authHandler, disposeHandler: false));
        auth.InitializeAsync().GetAwaiter().GetResult();

        var sendHandler = new MockHttpMessageHandler();
        var sender = new VkPlayMessageSender(config.Object, new HttpClient(sendHandler, disposeHandler: false), auth);
        return (sender, sendHandler, auth, authHandler);
    }
}
