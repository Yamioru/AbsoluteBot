using AbsoluteBot.Services.UtilityServices;
using Serilog;

namespace AbsoluteBot.Services.ChatServices.VkPlayLive;

/// <summary>
/// Управляет подключением к сервису VkPlayLive, включая логику подключения, переподключения и отключения.
/// </summary>
public class VkPlayConnectionManager(WebSocketConnectionManager webSocketManager, ConfigService configService,
    VkPlayAuthService authService)
{
    internal const string AuthCookiePlaceholder = "{auth}";
    private const int ReconnectDelayMilliseconds = 5000;
    private const string VkPlayLiveUrl = "https://live.vkvideo.ru";
    private readonly Uri _vkPlayUri = new("wss://pubsub.live.vkvideo.ru/connection/websocket?cf_protocol_version=v2");
    private bool _isConfigured;
    private bool _isReconnecting;
    private string? _channelId;
    public bool IsConnected => webSocketManager.IsConnected;
    public event EventHandler? OnReconnectSuccess;

    /// <summary>
    /// Асинхронно подключает к WebSocket-серверу VkPlayLive и подписывается на чат-канал.
    /// </summary>
    public async Task ConnectAsync()
    {
        try
        {
            while (!IsConnected && _isConfigured)
            {
                await webSocketManager.ConnectAsync(_vkPlayUri, VkPlayLiveUrl).ConfigureAwait(false);

                if (IsConnected)
                {
                    var readToken = await authService.GetWebSocketConnectTokenAsync().ConfigureAwait(false);
                    if (string.IsNullOrEmpty(readToken))
                    {
                        Log.Warning("Не удалось получить WebSocket-токен VK Live.");
                        await webSocketManager.DisconnectAsync().ConfigureAwait(false);
                        await Task.Delay(ReconnectDelayMilliseconds).ConfigureAwait(false);
                        continue;
                    }

                    if (string.IsNullOrEmpty(_channelId))
                    {
                        Log.Warning("VK Live: VkPlayChannelId пуст, подписка на чат пропущена.");
                        await webSocketManager.DisconnectAsync().ConfigureAwait(false);
                        await Task.Delay(ReconnectDelayMilliseconds).ConfigureAwait(false);
                        continue;
                    }

                    var channelId = _channelId;
                    await webSocketManager.SendMessageAsync(VkPlayCentrifugoProtocol.ConnectPayload(readToken))
                        .ConfigureAwait(false);
                    Log.Information("VK Live: отправлен Centrifugo connect, channelId={ChannelId}.", channelId);

                    foreach (var (id, channel) in VkPlayCentrifugoProtocol.ChatSubscriptions(channelId))
                    {
                        await webSocketManager.SendMessageAsync(VkPlayCentrifugoProtocol.SubscribePayload(id, channel))
                            .ConfigureAwait(false);
                        Log.Information("VK Live: подписка id={Id} на {Channel}.", id, channel);
                    }

                    OnReconnectSuccess?.Invoke(this, EventArgs.Empty);
                }

                if (!IsConnected)
                    await Task.Delay(ReconnectDelayMilliseconds).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Log.ForContext("ConnectionEvent", true).Error(ex, "Ошибка при подключении ");
            throw;
        }
    }

    /// <summary>
    /// Отключает соединение с сервером VkPlayLive.
    /// </summary>
    public async Task DisconnectAsync()
    {
        await webSocketManager.DisconnectAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Собирает Cookie-заголовок из шаблона. Плейсхолдер <c>{auth}</c> заменяется на auth-куку;
    /// если плейсхолдера нет, auth дописывается в конец.
    /// </summary>
    internal static string BuildCookieHeader(string template, string? authCookie)
    {
        authCookie ??= string.Empty;
        if (template.Contains(AuthCookiePlaceholder, StringComparison.Ordinal))
            return template.Replace(AuthCookiePlaceholder, authCookie, StringComparison.Ordinal);
        if (string.IsNullOrEmpty(authCookie)) return template;
        return $"{template.TrimEnd().TrimEnd(';')}; {authCookie}";
    }

    public async Task<bool> InitializeAsync()
    {
        _channelId = await configService.GetConfigValueAsync<string>("VkPlayChannelId").ConfigureAwait(false);
        if (string.IsNullOrEmpty(_channelId))
        {
            Log.Warning("Не удалось загрузить VkPlayChannelId.");
            return false;
        }

        _isConfigured = true;
        return true;
    }

    /// <summary>
    /// Асинхронно переподключает к WebSocket-серверу VkPlayLive.
    /// </summary>
    public async Task ReconnectAsync()
    {
        if (_isReconnecting) return;
        _isReconnecting = true;

        try
        {
            Log.ForContext("ConnectionEvent", true).Information("Попытка переподключения к VkPlayLive...");
            await DisconnectAsync().ConfigureAwait(false);
            await ConnectAsync().ConfigureAwait(false);
            Log.ForContext("ConnectionEvent", true).Information("Переподключение к VkPlayLive успешно.");
        }
        catch (Exception ex)
        {
            Log.ForContext("ConnectionEvent", true).Error(ex, "Ошибка при попытке переподключения.");
        }

        _isReconnecting = false;
    }
}
