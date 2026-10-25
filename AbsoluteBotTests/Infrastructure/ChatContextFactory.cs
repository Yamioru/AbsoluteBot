using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services.ChatServices.Interfaces;
using Telegram.Bot.Types;

namespace AbsoluteBotTests.Infrastructure;

public static class ChatContextFactory
{
    public static TextChatContext CreateTextContext(
        IChatService? chatService = null,
        string username = "testuser",
        int maxMessageLength = 500,
        List<string>? lastMessages = null)
    {
        return new TextChatContext(username, maxMessageLength, chatService ?? new FakeChatService(), lastMessages, null);
    }

    public static TelegramChatContext CreateTelegramContext(
        ChannelType channelType,
        IChatService? chatService = null,
        string username = "testuser",
        int maxMessageLength = 500)
    {
        return new TelegramChatContext(
            username,
            maxMessageLength,
            chatService ?? new FakeChatService(),
            channelId: 1,
            channelType,
            messageId: 1,
            new Message(),
            lastMessages: null,
            replyInfo: null);
    }

    public static TwitchChatContext CreateTwitchContext(
        IChatService? chatService = null,
        string username = "testuser",
        int maxMessageLength = 500)
    {
        return new TwitchChatContext(
            username,
            maxMessageLength,
            chatService ?? new FakeChatService(),
            channel: "testchannel",
            messageId: "1",
            lastMessages: null,
            replyInfo: null,
            displayedName: username);
    }

    public static VkPlayChatContext CreateVkPlayContext(
        IChatService? chatService = null,
        string username = "testuser",
        int maxMessageLength = 500)
    {
        return new VkPlayChatContext(
            username,
            maxMessageLength,
            chatService ?? new FakeChatService(),
            lastMessages: null,
            replyInfo: null,
            mentionNicknames: new List<string>(),
            messageId: 1,
            urls: new List<string>());
    }

    public static ParsedCommand CreateParsed(
        string commandName,
        string parameters = "",
        IChatService? chatService = null,
        UserRole role = UserRole.Default,
        string username = "testuser",
        ChatContext? context = null)
    {
        var service = chatService ?? new FakeChatService();
        context ??= CreateTextContext(service, username);
        return new ParsedCommand(commandName, parameters, context, role);
    }

    public static ParsedCommand CreateAdminChannelParsed(
        string commandName,
        string parameters = "",
        IChatService? chatService = null,
        UserRole role = UserRole.Administrator,
        string username = "admin")
    {
        var context = CreateTelegramContext(ChannelType.Administrative, chatService, username);
        return new ParsedCommand(commandName, parameters, context, role);
    }

    public static ParsedCommand CreateStreamingParsed(
        string commandName,
        string parameters = "",
        IChatService? chatService = null,
        UserRole role = UserRole.Default,
        string username = "testuser")
    {
        var context = CreateTwitchContext(chatService, username);
        return new ParsedCommand(commandName, parameters, context, role);
    }
}
