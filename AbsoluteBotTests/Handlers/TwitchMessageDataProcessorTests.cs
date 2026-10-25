using System.Drawing;
using System.Runtime.CompilerServices;
using AbsoluteBot.Services.ChatServices.TwitchChat;
using AbsoluteBot.Services.UtilityServices;
using Moq;
using TwitchLib.Client.Enums;
using TwitchLib.Client.Models;

namespace AbsoluteBotTests.Handlers;

[Trait("Category", "Unit")]
[Trait("Area", "Handlers")]
public class TwitchMessageDataProcessorTests
{
    [Fact]
    public async Task IsOwnBotMessage_MatchesTwitchAndCommonBotNames()
    {
        var processor = await CreateProcessorAsync();

        Assert.True(processor.IsOwnBotMessage("twitchbot"));
        Assert.True(processor.IsOwnBotMessage("TwitchBot"));
        Assert.True(processor.IsOwnBotMessage("Sobeka"));
        Assert.False(processor.IsOwnBotMessage("alice"));
        Assert.False(processor.IsOwnBotMessage(null));
    }

    [Fact]
    public async Task TryParseValidMessage_IgnoresBotOwnMessages()
    {
        var processor = await CreateProcessorAsync();
        var chatService = (TwitchChatService)RuntimeHelpers.GetUninitializedObject(typeof(TwitchChatService));
        var botMessage = CreateChatMessage("twitchbot", "hello from bot");

        var ok = processor.TryParseValidMessage(botMessage, chatService, out _, out _);

        Assert.False(ok);
    }

    [Fact]
    public async Task TryParseValidMessage_IgnoresOwnCommands()
    {
        var processor = await CreateProcessorAsync();
        var chatService = (TwitchChatService)RuntimeHelpers.GetUninitializedObject(typeof(TwitchChatService));
        var botCommand = CreateChatMessage("twitchbot", "!факт");

        var ok = processor.TryParseValidMessage(botCommand, chatService, out _, out _);

        Assert.False(ok);
    }

    [Fact]
    public async Task TryParseValidMessage_ParsesUserMessage()
    {
        var processor = await CreateProcessorAsync();
        var chatService = (TwitchChatService)RuntimeHelpers.GetUninitializedObject(typeof(TwitchChatService));
        var userMessage = CreateChatMessage("alice", "hello chat", displayName: "Alice", id: "id-42");

        var ok = processor.TryParseValidMessage(userMessage, chatService, out var text, out var context);

        Assert.True(ok);
        Assert.Equal("hello chat", text);
        Assert.Equal("alice", context!.Username);
        Assert.Equal("Alice", context.DisplayedName);
        Assert.Equal("id-42", context.MessageId);
    }

    private static async Task<TwitchMessageDataProcessor> CreateProcessorAsync()
    {
        var config = new Mock<ConfigService>();
        config.Setup(c => c.GetConfigValueAsync<string>("BotName")).ReturnsAsync("Sobeka");
        config.Setup(c => c.GetConfigValueAsync<string>("TwitchBotName")).ReturnsAsync("twitchbot");
        var processor = new TwitchMessageDataProcessor(config.Object);
        await processor.InitializeAsync();
        return processor;
    }

    private static ChatMessage CreateChatMessage(string username, string message, string? displayName = null, string id = "msg-1")
    {
        return new ChatMessage(
            "twitchbot",
            "1",
            username,
            displayName ?? username,
            string.Empty,
            Color.Empty,
            null!,
            message,
            UserType.Viewer,
            "channel",
            id,
            false,
            0,
            "1",
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            Noisy.NotSet,
            string.Empty,
            string.Empty,
            new List<KeyValuePair<string, string>>(),
            null!,
            0,
            0);
    }
}
