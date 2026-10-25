using System.Net;
using AbsoluteBot.Services.ChatServices.VkPlayLive;
using AbsoluteBot.Services.UtilityServices;
using AbsoluteBotTests.Infrastructure;
using Moq;

namespace AbsoluteBotTests.Services;

[Trait("Category", "Unit")]
[Trait("Area", "Services")]
public class VkPlayAuthServiceTests
{
    private const string Access = "access-token-1";
    private const string Refresh = "refresh-token-1";
    private const string ClientId = "device-client-id";

    [Fact]
    public void ParseAuthCookie_PrefixedJson_ReadsCamelCaseFields()
    {
        var expires = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 60_000;
        var raw = $"auth={{\"accessToken\":\"{Access}\",\"refreshToken\":\"{Refresh}\",\"expiresAt\":{expires}}}";

        var parsed = VkPlayAuthService.ParseAuthCookie(raw);

        Assert.NotNull(parsed);
        Assert.Equal(Access, parsed.AccessToken);
        Assert.Equal(Refresh, parsed.RefreshToken);
        Assert.Equal(expires, parsed.ExpiresAtMs);
    }

    [Fact]
    public void ParseAuthCookie_BareJson_ReadsFields()
    {
        var parsed = VkPlayAuthService.ParseAuthCookie(
            "{\"accessToken\":\"a\",\"refreshToken\":\"r\",\"expiresAt\":1784713052193}");

        Assert.NotNull(parsed);
        Assert.Equal("a", parsed.AccessToken);
        Assert.Equal("r", parsed.RefreshToken);
        Assert.Equal(1784713052193, parsed.ExpiresAtMs);
    }

    [Fact]
    public void ParseAuthCookie_UrlEncoded_DecodesJson()
    {
        var json = "{\"accessToken\":\"enc-a\",\"refreshToken\":\"enc-r\",\"expiresAt\":1784713052193}";
        var encoded = "auth=" + Uri.EscapeDataString(json);

        var parsed = VkPlayAuthService.ParseAuthCookie(encoded);

        Assert.NotNull(parsed);
        Assert.Equal("enc-a", parsed.AccessToken);
        Assert.Equal("enc-r", parsed.RefreshToken);
    }

    [Fact]
    public void ParseAuthCookie_ExpiresIn_ConvertsToFutureTimestamp()
    {
        var before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var parsed = VkPlayAuthService.ParseAuthCookie(
            "{\"access\":\"html-a\",\"refresh\":\"html-r\",\"expiresIn\":2592000}");
        var after = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        Assert.NotNull(parsed);
        Assert.Equal("html-a", parsed.AccessToken);
        Assert.Equal("html-r", parsed.RefreshToken);
        Assert.InRange(parsed.ExpiresAtMs, before + 2592000L * 1000, after + 2592000L * 1000);
    }

    [Fact]
    public void NormalizeExpiresAt_SecondsBecomeMilliseconds()
    {
        Assert.Equal(1_784_713_052_000, VkPlayAuthService.NormalizeExpiresAt(1_784_713_052));
        Assert.Equal(1_784_713_052_193, VkPlayAuthService.NormalizeExpiresAt(1_784_713_052_193));
    }

    [Fact]
    public void BuildAuthCookie_WritesMillisecondExpiresAt()
    {
        const long expires = 1_784_713_052_193;
        var cookie = VkPlayAuthService.BuildAuthCookie(new VkPlayAuthTokens(Access, Refresh, expires, ClientId));

        Assert.StartsWith("auth=", cookie);
        Assert.Contains("\"accessToken\":", cookie);
        Assert.Contains("\"refreshToken\":", cookie);
        Assert.Contains($"\"expiresAt\":{expires}", cookie);
        Assert.Contains($"\"clientId\":\"{ClientId}\"", cookie);
        Assert.DoesNotContain("expiresIn", cookie);
    }

    [Fact]
    public void ExtractClientIdFromCookieTemplate_ReadsValue()
    {
        var id = VkPlayAuthService.ExtractClientIdFromCookieTemplate(
            "theme=dark; _clientId=abc-123; {auth}; extra=1");

        Assert.Equal("abc-123", id);
    }

    [Fact]
    public void ExtractWsToken_RootAndNested()
    {
        Assert.Equal("ws-1", VkPlayAuthService.ExtractWsToken("{\"token\":\"ws-1\"}"));
        Assert.Equal("ws-2", VkPlayAuthService.ExtractWsToken("{\"data\":{\"token\":\"ws-2\"}}"));
        Assert.Null(VkPlayAuthService.ExtractWsToken("{}"));
    }

    [Fact]
    public async Task TryRefreshAsync_Success_PersistsAuthAndSendTokens()
    {
        var future = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 3_600_000;
        var (auth, handler, saved) = CreateAuth(new VkPlayAuthTokens(Access, Refresh, future, ClientId));
        using (auth)
        {
            await auth.InitializeAsync();
            handler.EnqueueResponse(HttpStatusCode.OK,
                "{\"access_token\":\"new-access\",\"refresh_token\":\"new-refresh\",\"expires_in\":2592000}");

            var ok = await auth.TryRefreshAsync();

            Assert.True(ok);
            Assert.Equal("new-access", auth.AccessToken);
            Assert.Equal("new-refresh", auth.RefreshToken);
            Assert.Equal("new-access", saved[VkPlayAuthService.AuthSendTokenConfigKey]);
            Assert.Contains("new-refresh", saved[VkPlayAuthService.AuthTokenConfigKey]);
            Assert.Contains("new-access", saved[VkPlayAuthService.AuthTokenConfigKey]);
            Assert.Equal(HttpMethod.Post, handler.LastMethod);
            Assert.Equal("https://api.live.vkvideo.ru/oauth/token/", handler.LastRequestUri);
            Assert.Contains("grant_type=refresh_token", handler.LastBody);
            Assert.Contains($"refresh_token={Refresh}", handler.LastBody);
            Assert.Contains($"device_id={ClientId}", handler.LastBody);
            Assert.Equal(0, auth.NotifyCount);
        }
    }

    [Fact]
    public async Task TryRefreshAsync_Unauthorized_ReturnsFalseWithoutThrowing()
    {
        var future = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 3_600_000;
        var (auth, handler, _) = CreateAuth(new VkPlayAuthTokens(Access, Refresh, future, ClientId));
        using (auth)
        {
            await auth.InitializeAsync();
            handler.EnqueueResponse(HttpStatusCode.Unauthorized, "{\"error\":\"invalid_grant\"}");

            var ok = await auth.TryRefreshAsync();

            Assert.False(ok);
            Assert.Equal(Access, auth.AccessToken);
            Assert.Equal(0, auth.NotifyCount);
        }
    }

    [Fact]
    public async Task GetWebSocketConnectTokenAsync_ReturnsToken()
    {
        var future = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 3_600_000;
        var (auth, handler, _) = CreateAuth(new VkPlayAuthTokens(Access, Refresh, future, ClientId));
        using (auth)
        {
            await auth.InitializeAsync();
            handler.EnqueueResponse(HttpStatusCode.OK, "{\"token\":\"ws-connect-token\"}");

            var token = await auth.GetWebSocketConnectTokenAsync();

            Assert.Equal("ws-connect-token", token);
            Assert.Equal(HttpMethod.Get, handler.LastMethod);
            Assert.Equal("https://api.live.vkvideo.ru/v1/ws/connect", handler.LastRequestUri);
            Assert.Equal($"Bearer {Access}", handler.LastAuthorization);
        }
    }

    [Fact]
    public async Task NotifyAuthFailedAsync_RespectsCooldown()
    {
        var future = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 3_600_000;
        var (auth, _, _) = CreateAuth(new VkPlayAuthTokens(Access, Refresh, future, ClientId));
        using (auth)
        {
            await auth.NotifyAuthFailedAsync();
            await auth.NotifyAuthFailedAsync();

            Assert.Equal(1, auth.NotifyCount);
        }
    }

    [Fact]
    public void ClampRefreshDelayMs_CapsToOneDay_AndFloorsToOneSecond()
    {
        Assert.Equal(1000, VkPlayAuthService.ClampRefreshDelayMs(-5_000));
        Assert.Equal(1000, VkPlayAuthService.ClampRefreshDelayMs(0));
        Assert.Equal(5_000, VkPlayAuthService.ClampRefreshDelayMs(5_000));
        Assert.Equal(VkPlayAuthService.MaxScheduleDelayMilliseconds,
            VkPlayAuthService.ClampRefreshDelayMs(60L * 24 * 60 * 60 * 1000));
        var delay = TimeSpan.FromMilliseconds(
            VkPlayAuthService.ClampRefreshDelayMs(180L * 24 * 60 * 60 * 1000));
        Assert.True(delay.TotalMilliseconds > 0);
        Assert.True(delay.TotalMilliseconds <= uint.MaxValue);
    }

    private static (VkPlayAuthService Auth, MockHttpMessageHandler Handler, Dictionary<string, string> Saved)
        CreateAuth(VkPlayAuthTokens tokens)
    {
        var saved = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [VkPlayAuthService.AuthTokenConfigKey] = VkPlayAuthService.BuildAuthCookie(tokens),
            [VkPlayAuthService.ClientIdConfigKey] = tokens.ClientId ?? string.Empty
        };

        var config = new Mock<ConfigService>();
        config.Setup(c => c.GetConfigValueAsync<string>(It.IsAny<string>()))
            .Returns((string key) => Task.FromResult(saved.GetValueOrDefault(key)));
        config.Setup(c => c.SetConfigValueAsync(It.IsAny<string>(), It.IsAny<object>()))
            .Callback<string, object>((key, value) => saved[key] = value.ToString() ?? string.Empty)
            .ReturnsAsync(true);

        var handler = new MockHttpMessageHandler();
        var httpClient = new HttpClient(handler, disposeHandler: false);
        var auth = new VkPlayAuthService(config.Object, httpClient);
        return (auth, handler, saved);
    }
}
