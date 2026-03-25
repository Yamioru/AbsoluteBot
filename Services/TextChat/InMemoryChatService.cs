using System.Collections.Generic;
using System.Threading.Tasks;
using AbsoluteBot.Chat.Context;
using AbsoluteBot.Events;
using AbsoluteBot.Services.ChatServices.Interfaces;

namespace AbsoluteBot.Services.TextChat;

public sealed class InMemoryChatService : IChatService
{
    private readonly List<string> _sentMessages = new();

    public IReadOnlyList<string> SentMessages => _sentMessages;

    public string? LastSentMessage => _sentMessages.Count == 0 ? null : _sentMessages[^1];

    public event EventHandler<MessageReceivedEventArgs>? MessageReceived;

    public Task Connect()
    {
        // Заглушка: для HTTP-сервиса ничего подключать не нужно.
        return Task.CompletedTask;
    }

    public Task SendMessageAsync(string message, ChatContext context)
    {
        _sentMessages.Add(message);
        return Task.CompletedTask;
    }
}

