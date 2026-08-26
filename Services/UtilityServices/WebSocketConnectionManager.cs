using System.Net.WebSockets;
using System.Text;
using Serilog;

namespace AbsoluteBot.Services.UtilityServices;

/// <summary>
///     Управляет подключениями WebSocket, обеспечивая возможность отправки и получения сообщений.
/// </summary>
public class WebSocketConnectionManager : IDisposable
{
    private const int BufferSize = 1024 * 8;
    private ClientWebSocket _webSocket = new();
    private bool _disposed;

    public bool IsConnected => _webSocket.State == WebSocketState.Open;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _webSocket.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Подключает WebSocket к указанному URI с заданным заголовком Origin.
    ///     После закрытия создаётся новый <see cref="ClientWebSocket" /> — экземпляр нельзя переиспользовать.
    /// </summary>
    public async Task ConnectAsync(Uri uri, string origin = "")
    {
        await DisconnectAsync().ConfigureAwait(false);
        _webSocket.Dispose();
        _webSocket = new ClientWebSocket();
        if (!string.IsNullOrEmpty(origin)) _webSocket.Options.SetRequestHeader("Origin", origin);
        await _webSocket.ConnectAsync(uri, CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    ///     Отключает соединение с сервером, если оно открыто.
    /// </summary>
    public async Task DisconnectAsync()
    {
        try
        {
            if (_webSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None)
                    .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.ForContext("ConnectionEvent", true).Error(ex, "Ошибка при закрытии WebSocket.");
        }
    }

    /// <summary>
    ///     Асинхронно получает сообщение от WebSocket.
    /// </summary>
    public async Task<string> ReceiveMessageAsync()
    {
        var buffer = new byte[BufferSize];
        var stringBuilder = new StringBuilder();
        WebSocketReceiveResult result;

        do
        {
            result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None)
                .ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await DisconnectAsync().ConfigureAwait(false);
                return string.Empty;
            }

            stringBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
        } while (!result.EndOfMessage);

        return stringBuilder.ToString();
    }

    /// <summary>
    ///     Асинхронно отправляет сообщение через WebSocket.
    /// </summary>
    public async Task SendMessageAsync(string message)
    {
        var buffer = Encoding.UTF8.GetBytes(message);
        await _webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None)
            .ConfigureAwait(false);
    }
}
