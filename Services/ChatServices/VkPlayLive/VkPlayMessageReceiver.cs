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
    private int _receiveSession;
    private int _running;

    public VkPlayMessageReceiver(WebSocketConnectionManager webSocketManager, VkPlayConnectionManager connectionManager)
    {
        _webSocketManager = webSocketManager;
        _connectionManager = connectionManager;
        _connectionManager.OnReconnectSuccess += (_, _) => { StartReceivingMessages(); };
    }

    public bool IsConnected => _connectionManager.IsConnected;
    public event EventHandler<string>? OnMessageReceived;

    /// <summary>
    ///     Запускает цикл чтения WebSocket, если он ещё не идёт.
    /// </summary>
    public void StartReceivingMessages()
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0) return;
        var session = Interlocked.Increment(ref _receiveSession);
        _ = RunReceiveLoopAsync(session);
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

    private async Task RunReceiveLoopAsync(int session)
    {
        Log.Information("VK Live: цикл чтения WebSocket #{Session} запущен.", session);
        try
        {
            while (_webSocketManager.IsConnected && Volatile.Read(ref _receiveSession) == session)
                try
                {
                    var message = await _webSocketManager.ReceiveMessageAsync().ConfigureAwait(false);
                    if (Volatile.Read(ref _receiveSession) != session) return;

                    foreach (var part in SplitWebSocketPayload(message))
                    {
                        if (VkPlayCentrifugoProtocol.IsPing(part))
                        {
                            await _webSocketManager.SendMessageAsync("{}").ConfigureAwait(false);
                            continue;
                        }

                        LogCentrifugoFrame(part);
                        OnMessageReceived?.Invoke(this, part);
                    }
                }
                catch (Exception ex)
                {
                    if (Volatile.Read(ref _receiveSession) != session) return;
                    Log.Error(ex, "Ошибка при получении сообщения из VkPlayLive.");
                    Interlocked.Increment(ref _receiveSession);
                    Interlocked.Exchange(ref _running, 0);
                    await _connectionManager.ReconnectAsync().ConfigureAwait(false);
                    return;
                }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при получении сообщения из VkPlayLive.");
            if (Volatile.Read(ref _receiveSession) == session && !IsConnected)
            {
                Interlocked.Exchange(ref _running, 0);
                await _connectionManager.ReconnectAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            if (Volatile.Read(ref _receiveSession) == session)
            {
                Interlocked.CompareExchange(ref _running, 0, 1);
                Log.Information("VK Live: цикл чтения WebSocket #{Session} завершён, connected={Connected}.",
                    session, IsConnected);
            }
        }
    }

    private static void LogCentrifugoFrame(string part)
    {
        var frame = VkPlayCentrifugoProtocol.DescribeFrame(part);
        if (!frame.LogAtInformation) return;
        if (frame.Kind == "error")
            Log.Error("VK Live Centrifugo {Kind}: {Detail}", frame.Kind, frame.Detail);
        else
            Log.Information("VK Live Centrifugo {Kind}: {Detail}", frame.Kind, frame.Detail);
    }
}
