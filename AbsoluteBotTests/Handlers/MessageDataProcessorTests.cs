using System.Runtime.CompilerServices;
using AbsoluteBot.Services.ChatServices.TelegramChat;
using AbsoluteBot.Services.ChatServices.VkPlayLive;
using AbsoluteBot.Services.UtilityServices;
using AbsoluteBotTests.Infrastructure;
using Moq;
using Telegram.Bot.Types;

namespace AbsoluteBotTests.Handlers;

[Trait("Category", "Unit")]
[Trait("Area", "Handlers")]
public class TelegramMessageDataProcessorTests
{
    [Fact]
    public async Task GetReplyInfo_ReturnsNull_WhenNoReply()
    {
        var config = new Mock<ConfigService>();
        config.Setup(c => c.GetConfigValueAsync<string>("BotName")).ReturnsAsync("Sobeka");
        config.Setup(c => c.GetConfigValueAsync<string>("TelegramBotName")).ReturnsAsync("SobekaBot");
        var processor = new TelegramMessageDataProcessor(
            new Mock<TelegramChannelManager>(new Mock<ConfigService>().Object).Object,
            config.Object);
        await processor.InitializeAsync();

        Assert.Null(processor.GetReplyInfo(null));
    }

    [Fact]
    public async Task GetReplyInfo_ExtractsUsernameAndText()
    {
        var config = new Mock<ConfigService>();
        config.Setup(c => c.GetConfigValueAsync<string>("BotName")).ReturnsAsync("Sobeka");
        config.Setup(c => c.GetConfigValueAsync<string>("TelegramBotName")).ReturnsAsync("SobekaBot");
        var processor = new TelegramMessageDataProcessor(
            new Mock<TelegramChannelManager>(new Mock<ConfigService>().Object).Object,
            config.Object);
        await processor.InitializeAsync();

        var reply = new Message
        {
            Text = "parent text",
            From = new User { Username = "alice", Id = 1, IsBot = false, FirstName = "A" }
        };

        var info = processor.GetReplyInfo(reply);

        Assert.NotNull(info);
        Assert.Equal("alice", info!.Username);
        Assert.Equal("parent text", info.Message);
    }
}

[Trait("Category", "Unit")]
[Trait("Area", "Handlers")]
public class VkPlayMessageDataProcessorTests
{
    private static string BuildEnvelope(string username, string textContent)
    {
        var escaped = textContent.Replace("\\", "\\\\").Replace("\"", "\\\"");
        return $$"""
        {
          "push": {
            "pub": {
              "data": {
                "type": "message",
                "data": {
                  "id": 10,
                  "author": { "name": "{{username}}" },
                  "data": [
                    { "type": "text", "content": "{{escaped}}" }
                  ]
                }
              }
            }
          }
        }
        """;
    }

    [Fact]
    public async Task TryParseValidMessage_ParsesTextMessage()
    {
        var config = new Mock<ConfigService>();
        config.Setup(c => c.GetConfigValueAsync<string>("BotName")).ReturnsAsync("Sobeka");
        config.Setup(c => c.GetConfigValueAsync<string>("VkPlayBotName")).ReturnsAsync("vkbot");
        var processor = new VkPlayMessageDataProcessor(config.Object);
        await processor.InitializeAsync();

        var raw = BuildEnvelope("alice", """["hello world","unstyled",[]]""");
        var chatService = (VkPlayChatService)RuntimeHelpers.GetUninitializedObject(typeof(VkPlayChatService));

        var ok = processor.TryParseValidMessage(raw, chatService, out var text, out var context);

        Assert.True(ok);
        Assert.Equal("hello world", text);
        Assert.Equal("alice", context!.Username);
        Assert.Equal(10, context.MessageId);
    }

    [Fact]
    public async Task TryParseValidMessage_IgnoresBotOwnChat()
    {
        var config = new Mock<ConfigService>();
        config.Setup(c => c.GetConfigValueAsync<string>("BotName")).ReturnsAsync("Sobeka");
        config.Setup(c => c.GetConfigValueAsync<string>("VkPlayBotName")).ReturnsAsync("vkbot");
        var processor = new VkPlayMessageDataProcessor(config.Object);
        await processor.InitializeAsync();
        var chatService = (VkPlayChatService)RuntimeHelpers.GetUninitializedObject(typeof(VkPlayChatService));

        var chat = BuildEnvelope("vkbot", """["hi","unstyled",[]]""");
        Assert.False(processor.TryParseValidMessage(chat, chatService, out _, out _));

        var twitchEcho = BuildEnvelope("vkbot", """["[Twitch] alice: !факт","unstyled",[]]""");
        Assert.False(processor.TryParseValidMessage(twitchEcho, chatService, out _, out _));

        var command = BuildEnvelope("vkbot", """["!факт","unstyled",[]]""");
        Assert.False(processor.TryParseValidMessage(command, chatService, out _, out _));
    }

    [Fact]
    public async Task TryParseValidMessage_UsesNickWhenNameMissing()
    {
        var config = new Mock<ConfigService>();
        config.Setup(c => c.GetConfigValueAsync<string>("BotName")).ReturnsAsync("Sobeka");
        config.Setup(c => c.GetConfigValueAsync<string>("VkPlayBotName")).ReturnsAsync("vkbot");
        var processor = new VkPlayMessageDataProcessor(config.Object);
        await processor.InitializeAsync();
        var chatService = (VkPlayChatService)RuntimeHelpers.GetUninitializedObject(typeof(VkPlayChatService));

        var raw = """
        {
          "push": {
            "pub": {
              "data": {
                "type": "message",
                "data": {
                  "id": 10,
                  "author": { "nick": "Ифа Вишес" },
                  "data": [
                    { "type": "text", "content": "[\"привет\",\"unstyled\",[]]" }
                  ]
                }
              }
            }
          }
        }
        """;

        var ok = processor.TryParseValidMessage(raw, chatService, out var text, out var context);
        Assert.True(ok);
        Assert.Equal("привет", text);
        Assert.Equal("Ифа Вишес", context!.Username);
    }

    [Fact]
    public async Task TryParseValidMessage_ParsesMessageV8()
    {
        var config = new Mock<ConfigService>();
        config.Setup(c => c.GetConfigValueAsync<string>("BotName")).ReturnsAsync("Sobeka");
        config.Setup(c => c.GetConfigValueAsync<string>("VkPlayBotName")).ReturnsAsync("vkbot");
        var processor = new VkPlayMessageDataProcessor(config.Object);
        await processor.InitializeAsync();
        var chatService = (VkPlayChatService)RuntimeHelpers.GetUninitializedObject(typeof(VkPlayChatService));

        var raw = """
        {
          "push": {
            "channel": "channel-chat:10898237",
            "pub": {
              "data": {
                "type": "message_v8",
                "data": {
                  "chatMessageSend": {
                    "message": {
                      "id": "652047349",
                      "author": { "id": 12345, "nick": "Ифа Вишес" },
                      "text": "пуп",
                      "textData": [
                        { "text": { "type": "text", "content": "[\"пуп\",\"unstyled\",[]]", "modificator": "" } },
                        { "text": { "type": "text", "content": "", "modificator": "BLOCK_END" } }
                      ]
                    }
                  }
                }
              }
            }
          }
        }
        """;

        var ok = processor.TryParseValidMessage(raw, chatService, out var text, out var context);
        Assert.True(ok);
        Assert.Equal("пуп", text);
        Assert.Equal("Ифа Вишес", context!.Username);
        Assert.Equal(652047349, context.MessageId);
        Assert.Equal("12345", context.UserId);
    }

    [Fact]
    public async Task TryParseValidMessage_ReturnsFalse_OnInvalidJson()
    {
        var config = new Mock<ConfigService>();
        config.Setup(c => c.GetConfigValueAsync<string>("BotName")).ReturnsAsync("Sobeka");
        config.Setup(c => c.GetConfigValueAsync<string>("VkPlayBotName")).ReturnsAsync("vkbot");
        var processor = new VkPlayMessageDataProcessor(config.Object);
        await processor.InitializeAsync();
        var chatService = (VkPlayChatService)RuntimeHelpers.GetUninitializedObject(typeof(VkPlayChatService));

        Assert.False(processor.TryParseValidMessage("{not-json", chatService, out _, out _));
    }

    [Fact]
    public async Task TryParseValidMessage_ReturnsFalse_OnConnectSubscribeHandshake()
    {
        var config = new Mock<ConfigService>();
        config.Setup(c => c.GetConfigValueAsync<string>("BotName")).ReturnsAsync("Sobeka");
        config.Setup(c => c.GetConfigValueAsync<string>("VkPlayBotName")).ReturnsAsync("vkbot");
        var processor = new VkPlayMessageDataProcessor(config.Object);
        await processor.InitializeAsync();
        var chatService = (VkPlayChatService)RuntimeHelpers.GetUninitializedObject(typeof(VkPlayChatService));

        const string handshake =
            """{"id":1,"connect":{"client":"cdafc3e7-e334-433a-907d-c2b5872cf093","version":"0.0.0","expires":true,"ttl":2592000,"ping":25,"pong":true}}""";

        Assert.False(processor.TryParseValidMessage(handshake, chatService, out _, out _));
    }
}
