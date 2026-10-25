using AbsoluteBot.Chat.Context;

namespace AbsoluteBot.Events;

/// <summary>
///     Аргументы события, содержащие текст сообщения и контекст чата.
/// </summary>
/// <param name="text">Текст сообщения, полученного в чате.</param>
/// <param name="context">Контекст чата, в котором было получено сообщение.</param>
public class MessageReceivedEventArgs(string text, ChatContext context) : EventArgs
{
    /// <summary>
    ///     Контекст чата, в котором было получено сообщение.
    /// </summary>
    public ChatContext Context { get; set; } = context;
    /// <summary>
    ///     Текст сообщения, полученного в чате.
    /// </summary>
    public string Text { get; set; } = text;
}