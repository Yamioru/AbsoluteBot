using AbsoluteBot.Services.UtilityServices;
using Serilog;

namespace AbsoluteBot.Services.ChatServices.VkPlayLive;

/// <summary>
///     Отвечает за получение сообщений из VkPlayLive через WebSocket.
/// </summary>
public class VkPlayMessageReceiver
{
    private readonly VkPlayConnectionManager _connectionManager;
    private readonly WebSocketConnectionManager _webSocketManager;
    private bool _isReceivingMessages;

    public VkPlayMessageReceiver(WebSocketConnectionManager webSocketManager, VkPlayConnectionManager connectionManager)
    {
        _webSocketManager = webSocketManager;
        _connectionManager = connectionManager;

        // Подписка на событие успешного переподключения
        _connectionManager.OnReconnectSuccess += (_, _) => { StartReceivingMessages(); };
    }

    /// <summary>
    ///     Проверяет, подключен ли WebSocket к серверу VkPlayLive.
    /// </summary>
    public bool IsConnected => _connectionManager.IsConnected;
    public event EventHandler<string>? OnMessageReceived;

    /// <summary>
    ///     Запускает процесс получения сообщений, если он еще не был запущен.
    /// </summary>
    public async void StartReceivingMessages()
    {
        if (_isReceivingMessages) return;
        _isReceivingMessages = true;

        try
        {
            await ReceiveMessagesAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при получении сообщения из VkPlayLive.");
            // Попытка переподключения после ошибки
            if (!IsConnected)
            {
                Log.Warning("Соединение потеряно. Инициация переподключения...");
                await _connectionManager.ReconnectAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            _isReceivingMessages = false;
        }
    }

    /// <summary>
    ///     Centrifugo может прислать несколько JSON в одном кадре, через перевод строки
    ///     (ответы connect+subscribe при старте).
    /// </summary>
    internal static IReadOnlyList<string> SplitWebSocketPayload(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<string>();
        if (!raw.Contains('\n')) return new[] {raw};

        var parts = new List<string>();
        foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (line.Length > 0)
                parts.Add(line);
        return parts;
    }

    /// <summary>
    ///     Асинхронно получает сообщения через WebSocket и обрабатывает их.
    /// </summary>
    private async Task ReceiveMessagesAsync()
    {
        while (_webSocketManager.IsConnected)
            try
            {
                var message = await _webSocketManager.ReceiveMessageAsync().ConfigureAwait(false);

                foreach (var part in SplitWebSocketPayload(message))
                    if (part == "{}")
                        await _webSocketManager.SendMessageAsync("{}").ConfigureAwait(false);
                    else
                        OnMessageReceived?.Invoke(this, part);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при получении сообщения из VkPlayLive.");
                await _connectionManager.ReconnectAsync().ConfigureAwait(false);
            }
    }
}