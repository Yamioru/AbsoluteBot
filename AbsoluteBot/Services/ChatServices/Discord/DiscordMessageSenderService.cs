using AbsoluteBot.Chat.Context;
using Discord;
using Discord.WebSocket;
using Serilog;

namespace AbsoluteBot.Services.ChatServices.Discord;

/// <summary>
///     Сервис для отправки сообщений в Discord каналы.
/// </summary>
public class DiscordMessageSenderService(DiscordSocketClient? client)
{
    /// <summary>
    ///     Асинхронно отправляет сообщение в указанный контекст чата Discord.
    /// </summary>
    /// <param name="message">Текст сообщения для отправки.</param>
    /// <param name="context">Контекст чата, в который отправляется сообщение.</param>
    public static async Task SendMessageAsync(string message, ChatContext context)
    {
        try
        {
            if (context is not DiscordChatContext discordChatContext || discordChatContext.UserMessage == null) return;

            // Разделяет сообщение на части, если оно слишком длинное
            var parts = SplitMessageIntoParts(message, DiscordChatService.MaxMessageLength);
            foreach (var part in parts)
                await discordChatContext.UserMessage.ReplyAsync(part).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при отправке сообщения в Discord.");
        }
    }

    /// <summary>
    ///     Асинхронно отправляет сообщение в указанный канал Discord по его идентификатору.
    /// </summary>
    /// <param name="channelId">Идентификатор канала.</param>
    /// <param name="message">Текст сообщения.</param>
    public async Task SendMessageToChannelAsync(ulong channelId, string message)
    {
        try
        {
            if (client?.GetChannel(channelId) is IMessageChannel channel)
            {
                // Разделяет сообщение на части, если оно слишком длинное
                var parts = SplitMessageIntoParts(message, DiscordChatService.MaxMessageLength);
                foreach (var part in parts)
                    await channel.SendMessageAsync($"{part}").ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при отправке сообщения на определенный канал в Discord.");
        }
    }

    /// <summary>
    ///     Извлекает часть сообщения до указанного индекса.
    /// </summary>
    /// <param name="message">Текст сообщения.</param>
    /// <param name="splitIndex">Индекс разделения.</param>
    /// <returns>Извлечённая часть сообщения.</returns>
    private static string ExtractPart(string message, int splitIndex)
    {
        return message[..splitIndex].Trim();
    }

    /// <summary>
    ///     Находит индекс для разделения сообщения.
    /// </summary>
    /// <param name="message">Текст сообщения для анализа.</param>
    /// <param name="maxLength">Максимальная длина части сообщения.</param>
    /// <returns>Индекс, по которому нужно разделить сообщение.</returns>
    private static int FindSplitIndex(string message, int maxLength)
    {
        var splitIndex = message.LastIndexOf('\n', maxLength);

        if (splitIndex == -1)
        {
            splitIndex = message.LastIndexOf(' ', maxLength);
            if (splitIndex == -1) splitIndex = maxLength;
        }

        return splitIndex;
    }

    /// <summary>
    ///     Разделяет сообщение на части, если его длина превышает максимальную длину.
    /// </summary>
    /// <param name="message">Текст сообщения для разделения.</param>
    /// <param name="maxLength">Максимальная длина одной части сообщения.</param>
    /// <returns>Список строк, каждая из которых является частью исходного сообщения.</returns>
    private static List<string> SplitMessageIntoParts(string message, int maxLength)
    {
        var parts = new List<string>();

        while (message.Length > maxLength)
        {
            var splitIndex = FindSplitIndex(message, maxLength);
            var part = ExtractPart(message, splitIndex);
            parts.Add(part);
            message = TrimMessage(message, splitIndex);
        }

        if (message.Length > 0) parts.Add(message);

        return parts;
    }

    /// <summary>
    ///     Обрезает сообщение, начиная с указанного индекса.
    /// </summary>
    /// <param name="message">Текст сообщения.</param>
    /// <param name="splitIndex">Индекс разделения.</param>
    /// <returns>Обрезанное сообщение.</returns>
    private static string TrimMessage(string message, int splitIndex)
    {
        return message[splitIndex..].TrimStart('\n', ' ');
    }
}