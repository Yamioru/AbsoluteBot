namespace AbsoluteBot.Services.ChatServices.TwitchChat;

/// <summary>
///     Отсекает повторную обработку одного и того же Twitch-сообщения (двойной IRC-клиент, повторный OnMessageReceived).
/// </summary>
internal sealed class TwitchMessageIdDeduplicator(int capacity = 500)
{
    private readonly HashSet<string> _ids = new(StringComparer.Ordinal);
    private readonly object _lock = new();
    private readonly Queue<string> _order = new();

    /// <summary>
    ///     Возвращает <c>true</c>, если сообщение с этим id нужно обработать впервые.
    ///     Пустой id не считается дублем.
    /// </summary>
    public bool TryTake(string? messageId)
    {
        if (string.IsNullOrEmpty(messageId)) return true;

        lock (_lock)
        {
            if (!_ids.Add(messageId)) return false;

            _order.Enqueue(messageId);
            while (_order.Count > capacity)
            {
                var oldest = _order.Dequeue();
                _ids.Remove(oldest);
            }

            return true;
        }
    }
}
