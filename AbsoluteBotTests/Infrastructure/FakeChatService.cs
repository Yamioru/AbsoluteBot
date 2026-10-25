using AbsoluteBot.Chat.Context;
using AbsoluteBot.Events;
using AbsoluteBot.Services.ChatServices.Interfaces;

namespace AbsoluteBotTests.Infrastructure;

/// <summary>
///     Тестовый IChatService без реального Connect к платформам.
/// </summary>
public sealed class FakeChatService : IChatService
{
    public event EventHandler<MessageReceivedEventArgs>? MessageReceived;

    public bool ConnectCalled { get; private set; }
    public List<(string Message, ChatContext Context)> SentMessages { get; } = new();

    public Task Connect()
    {
        ConnectCalled = true;
        return Task.CompletedTask;
    }

    public Task SendMessageAsync(string message, ChatContext context)
    {
        SentMessages.Add((message, context));
        return Task.CompletedTask;
    }

    public void RaiseMessageReceived(string text, ChatContext context)
    {
        MessageReceived?.Invoke(this, new MessageReceivedEventArgs(text, context));
    }
}
