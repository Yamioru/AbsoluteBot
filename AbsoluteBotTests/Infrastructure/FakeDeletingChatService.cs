using AbsoluteBot.Chat.Context;
using AbsoluteBot.Events;
using AbsoluteBot.Services.ChatServices.Interfaces;

namespace AbsoluteBotTests.Infrastructure;

/// <summary>
///     Fake IChatService that also supports message deletion.
/// </summary>
public sealed class FakeDeletingChatService : IChatService, ISupportsMessageDeletion
{
    public event EventHandler<MessageReceivedEventArgs>? MessageReceived;

    public List<(string Message, ChatContext Context)> SentMessages { get; } = new();
    public List<(ChatContext Context, int Count)> DeletedBatches { get; } = new();

    public Task Connect() => Task.CompletedTask;

    public Task SendMessageAsync(string message, ChatContext context)
    {
        SentMessages.Add((message, context));
        return Task.CompletedTask;
    }

    public Task DeleteMessagesAsync(ChatContext context, int count)
    {
        DeletedBatches.Add((context, count));
        return Task.CompletedTask;
    }
}
