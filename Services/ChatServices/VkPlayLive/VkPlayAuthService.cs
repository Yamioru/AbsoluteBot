using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AbsoluteBot.Chat.Context;
using AbsoluteBot.Services.ChatServices.TelegramChat;
using AbsoluteBot.Services.UtilityServices;
using Serilog;

namespace AbsoluteBot.Services.ChatServices.VkPlayLive;

/// <summary>
///     Хранит и обновляет сессию VK Live: cookie <c>auth</c>, Bearer для отправки и WebSocket-токен.
///     Access-токен продлевается через <c>POST /oauth/token/</c>; если refresh окончательно не удался,
///     в административный канал Telegram уходит уведомление.
/// </summary>
public partial class VkPlayAuthService : IAsyncInitializable, IDisposable
{
    internal const string AuthTokenConfigKey = "VkPlayAuthToken";
    internal const string AuthSendTokenConfigKey = "VkPlayAuthSendToken";
    internal const string ClientIdConfigKey = "VkPlayClientId";
    internal const string RequestCookiesConfigKey = "VkPlayRequestCookies";
    internal const string RequestCookiesFileName = "vkplay_request_cookies.txt";
    internal const long MillisecondTimestampThreshold = 1_000_000_000_000L;
    internal const int TokenExpiresShiftMilliseconds = 10 * 60 * 1000;

    private const string RefreshUrl = "https://api.live.vkvideo.ru/oauth/token/";
    private const string WsConnectUrl = "https://api.live.vkvideo.ru/v1/ws/connect";
    private const string LiveOrigin = "https://live.vkvideo.ru";
    private const string DeviceOs = "streams_web";
    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/127.0.0.0 Safari/537.36";

    private static readonly JsonSerializerOptions CookieJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions ApiJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ConfigService _configService;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly TelegramChannelManager? _telegramChannelManager;
    private readonly TelegramChatService? _telegramChatService;
    private CancellationTokenSource? _refreshCts;
    private DateTime _lastNotifyUtc = DateTime.MinValue;
    private bool _disposed;

    public VkPlayAuthService(ConfigService configService, HttpClient httpClient,
        TelegramChatService? telegramChatService = null, TelegramChannelManager? telegramChannelManager = null)
    {
        _configService = configService;
        _httpClient = httpClient;
        _telegramChatService = telegramChatService;
        _telegramChannelManager = telegramChannelManager;
        EnsureDefaultHeaders(_httpClient);
    }

    internal TimeSpan NotifyCooldown { get; set; } = TimeSpan.FromHours(1);
    internal int NotifyCount { get; private set; }
    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public long ExpiresAtMs { get; private set; }
    public string? ClientId { get; private set; }

    public async Task InitializeAsync()
    {
        ClientId = await LoadClientIdAsync().ConfigureAwait(false);
        var rawCookie = await _configService.GetConfigValueAsync<string>(AuthTokenConfigKey).ConfigureAwait(false);
        ApplyTokens(ParseAuthCookie(rawCookie));

        if (string.IsNullOrEmpty(AccessToken) && string.IsNullOrEmpty(RefreshToken))
        {
            Log.Warning("Не задан VkPlayAuthToken: нужна cookie auth с live.vkvideo.ru.");
            return;
        }

        if (IsAccessTokenExpired() || string.IsNullOrEmpty(AccessToken))
            await TryRefreshAsync().ConfigureAwait(false);
        else
            ScheduleRefresh();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _refreshLock.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Разбирает cookie <c>auth</c>: префикс <c>auth=</c>, голый JSON, URL-encoded JSON,
    ///     поля <c>accessToken</c>/<c>access</c>, <c>refreshToken</c>/<c>refresh</c>, <c>expiresAt</c>/<c>expiresIn</c>.
    /// </summary>
    internal static VkPlayAuthTokens? ParseAuthCookie(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var value = raw.Trim();
        if (value.StartsWith("auth=", StringComparison.OrdinalIgnoreCase))
            value = value["auth=".Length..];
        value = value.Trim().TrimEnd(';').Trim();
        if (string.IsNullOrEmpty(value)) return null;

        value = DecodeIfUrlEncoded(value);

        try
        {
            using var doc = JsonDocument.Parse(value);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;

            var access = GetString(root, "accessToken") ?? GetString(root, "access");
            var refresh = GetString(root, "refreshToken") ?? GetString(root, "refresh");
            var clientId = GetString(root, "clientId");
            long expiresAtMs = 0;
            if (TryGetInt64(root, "expiresAt", out var expiresAt))
                expiresAtMs = NormalizeExpiresAt(expiresAt);
            else if (TryGetInt64(root, "expiresIn", out var expiresIn))
                expiresAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + expiresIn * 1000;

            if (string.IsNullOrEmpty(access) && string.IsNullOrEmpty(refresh)) return null;
            return new VkPlayAuthTokens(access, refresh, expiresAtMs, clientId);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    ///     Собирает cookie <c>auth={json}</c> с миллисекундным <c>expiresAt</c>.
    /// </summary>
    internal static string BuildAuthCookie(VkPlayAuthTokens tokens)
    {
        var dto = new VkPlayAuthCookieDto
        {
            AccessToken = tokens.AccessToken,
            RefreshToken = tokens.RefreshToken,
            ExpiresAt = tokens.ExpiresAtMs,
            ClientId = string.IsNullOrEmpty(tokens.ClientId) ? null : tokens.ClientId
        };
        return "auth=" + JsonSerializer.Serialize(dto, CookieJsonOptions);
    }

    internal static long NormalizeExpiresAt(long value)
    {
        return value > MillisecondTimestampThreshold ? value : value * 1000;
    }

    internal static string? ExtractClientIdFromCookieTemplate(string? template)
    {
        if (string.IsNullOrWhiteSpace(template)) return null;
        var match = ClientIdRegex().Match(template);
        if (!match.Success) return null;
        var value = match.Groups[1].Value.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    public bool IsAccessTokenExpired()
    {
        if (string.IsNullOrEmpty(AccessToken) || ExpiresAtMs <= 0) return true;
        return ExpiresAtMs - TokenExpiresShiftMilliseconds <= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    /// <summary>
    ///     Обновляет access/refresh через OAuth. Всегда ходит в API (в том числе после 401, даже если по часам токен ещё жив).
    /// </summary>
    public async Task<bool> TryRefreshAsync()
    {
        await _refreshLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (string.IsNullOrEmpty(RefreshToken))
            {
                Log.Warning("Нет refresh-токена VK Live — автообновление невозможно.");
                return false;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, RefreshUrl);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["response_type"] = "code",
                ["refresh_token"] = RefreshToken,
                ["grant_type"] = "refresh_token",
                ["device_id"] = ClientId ?? string.Empty,
                ["device_os"] = DeviceOs
            });

            using var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                Log.Error("Не удалось обновить токен VK Live: {Status} {Body}", (int)response.StatusCode, Truncate(body));
                return false;
            }

            var parsed = JsonSerializer.Deserialize<RefreshTokenResponse>(body, ApiJsonOptions);
            if (parsed == null || string.IsNullOrEmpty(parsed.AccessToken) || string.IsNullOrEmpty(parsed.RefreshToken))
            {
                Log.Error("Ответ refresh VK Live без access_token/refresh_token.");
                return false;
            }

            var expiresAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + parsed.ExpiresIn * 1000L;
            ApplyTokens(new VkPlayAuthTokens(parsed.AccessToken, parsed.RefreshToken, expiresAtMs, ClientId));
            await PersistTokensAsync().ConfigureAwait(false);
            ScheduleRefresh();
            Log.Information("Токен VK Live успешно обновлён, действует до {ExpiresAt}.",
                DateTimeOffset.FromUnixTimeMilliseconds(ExpiresAtMs));
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при обновлении токена VK Live.");
            return false;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <summary>
    ///     Возвращает токен для Centrifugo <c>connect</c>. При необходимости сначала обновляет access-токен.
    /// </summary>
    public async Task<string?> GetWebSocketConnectTokenAsync()
    {
        if (IsAccessTokenExpired())
        {
            if (!await TryRefreshAsync().ConfigureAwait(false))
                return null;
        }

        var token = await RequestWebSocketConnectTokenAsync().ConfigureAwait(false);
        if (!string.IsNullOrEmpty(token)) return token;

        if (await TryRefreshAsync().ConfigureAwait(false))
            token = await RequestWebSocketConnectTokenAsync().ConfigureAwait(false);

        if (string.IsNullOrEmpty(token))
            await NotifyAuthFailedAsync().ConfigureAwait(false);

        return token;
    }

    public async Task NotifyAuthFailedAsync()
    {
        var now = DateTime.UtcNow;
        if (now - _lastNotifyUtc < NotifyCooldown) return;
        _lastNotifyUtc = now;
        NotifyCount++;

        Log.Warning("Сессия VK Live слетела. Нужна новая cookie auth из браузера.");
        if (_telegramChatService == null || _telegramChannelManager == null) return;

        try
        {
            var channelId = await _telegramChannelManager.GetTelegramChannelId(ChannelType.Administrative)
                .ConfigureAwait(false);
            if (channelId == 0) return;

            await _telegramChatService.SendMessageToChannelAsync(
                "VK Live: токен авторизации слетел. Автообновление не удалось. Скопируйте cookie auth с live.vkvideo.ru и выполните !setconfig VkPlayAuthToken <значение>, затем !перезагрузка.",
                channelId.ToString()).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Не удалось отправить уведомление об истечении токена VK Live в Telegram.");
        }
    }

    private async Task<string?> RequestWebSocketConnectTokenAsync()
    {
        if (string.IsNullOrEmpty(AccessToken)) return null;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, WsConnectUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
            if (!string.IsNullOrEmpty(ClientId))
                request.Headers.TryAddWithoutValidation("X-From-Id", ClientId);

            using var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                Log.Warning("GET /v1/ws/connect вернул {Status}.", (int)response.StatusCode);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                Log.Error("GET /v1/ws/connect: {Status} {Body}", (int)response.StatusCode, Truncate(body));
                return null;
            }

            return ExtractWsToken(body);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при получении WebSocket-токена VK Live.");
            return null;
        }
    }

    internal static string? ExtractWsToken(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("token", out var tokenEl) && tokenEl.ValueKind == JsonValueKind.String)
                return tokenEl.GetString();
            if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object &&
                data.TryGetProperty("token", out var nested) && nested.ValueKind == JsonValueKind.String)
                return nested.GetString();
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private void ApplyTokens(VkPlayAuthTokens? tokens)
    {
        if (tokens == null) return;
        AccessToken = tokens.AccessToken;
        RefreshToken = tokens.RefreshToken;
        ExpiresAtMs = tokens.ExpiresAtMs;
        if (!string.IsNullOrEmpty(tokens.ClientId))
            ClientId = tokens.ClientId;
    }

    private async Task PersistTokensAsync()
    {
        var cookie = BuildAuthCookie(new VkPlayAuthTokens(AccessToken, RefreshToken, ExpiresAtMs, ClientId));
        await _configService.SetConfigValueAsync(AuthTokenConfigKey, cookie).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(AccessToken))
            await _configService.SetConfigValueAsync(AuthSendTokenConfigKey, AccessToken).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(ClientId))
            await _configService.SetConfigValueAsync(ClientIdConfigKey, ClientId).ConfigureAwait(false);
    }

    private async Task<string?> LoadClientIdAsync()
    {
        var fromConfig = await _configService.GetConfigValueAsync<string>(ClientIdConfigKey).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(fromConfig)) return fromConfig.Trim();

        var template = await LoadRequestCookiesTemplateAsync().ConfigureAwait(false);
        return ExtractClientIdFromCookieTemplate(template);
    }

    private async Task<string?> LoadRequestCookiesTemplateAsync()
    {
        var filePath = DataPaths.Get(RequestCookiesFileName);
        if (File.Exists(filePath))
        {
            var fromFile = (await File.ReadAllTextAsync(filePath).ConfigureAwait(false)).Trim();
            if (!string.IsNullOrEmpty(fromFile)) return fromFile;
        }

        var fromConfig = await _configService.GetConfigValueAsync<string>(RequestCookiesConfigKey).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(fromConfig) ? null : fromConfig.Trim();
    }

    private void ScheduleRefresh()
    {
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _refreshCts = new CancellationTokenSource();
        var delayMs = ExpiresAtMs - TokenExpiresShiftMilliseconds - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (delayMs < 1000) delayMs = 1000;
        var token = _refreshCts.Token;
        _ = RefreshWhenDueAsync(TimeSpan.FromMilliseconds(delayMs), token);
    }

    private async Task RefreshWhenDueAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            if (!await TryRefreshAsync().ConfigureAwait(false))
                await NotifyAuthFailedAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // штатная отмена при Dispose / новом расписании
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка отложенного обновления токена VK Live.");
        }
    }

    private static void EnsureDefaultHeaders(HttpClient httpClient)
    {
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", UserAgent);
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Origin", LiveOrigin);
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Referer", LiveOrigin);
    }

    private static string DecodeIfUrlEncoded(string value)
    {
        if (!value.Contains('%', StringComparison.Ordinal)) return value;
        try
        {
            var decoded = Uri.UnescapeDataString(value);
            return decoded.Contains('{', StringComparison.Ordinal) ? decoded : value;
        }
        catch (UriFormatException)
        {
            return value;
        }
    }

    private static string? GetString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el)) return null;
        return el.ValueKind == JsonValueKind.String ? el.GetString() : el.ToString();
    }

    private static bool TryGetInt64(JsonElement root, string name, out long value)
    {
        value = 0;
        if (!root.TryGetProperty(name, out var el)) return false;
        switch (el.ValueKind)
        {
            case JsonValueKind.Number:
                return el.TryGetInt64(out value);
            case JsonValueKind.String:
                return long.TryParse(el.GetString(), out value);
            default:
                return false;
        }
    }

    private static string Truncate(string text, int max = 200)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= max) return text;
        return text[..max];
    }

    [GeneratedRegex(@"_clientId=([^;]+)", RegexOptions.IgnoreCase)]
    private static partial Regex ClientIdRegex();

    private sealed class VkPlayAuthCookieDto
    {
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public long ExpiresAt { get; set; }
        public string? ClientId { get; set; }
    }

    private sealed class RefreshTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}

/// <summary>
///     Разобранные поля сессии VK Live.
/// </summary>
internal sealed record VkPlayAuthTokens(string? AccessToken, string? RefreshToken, long ExpiresAtMs, string? ClientId);
