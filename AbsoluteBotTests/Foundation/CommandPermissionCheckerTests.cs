using AbsoluteBot.Chat.Commands;
using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBotTests.Infrastructure;
using Telegram.Bot.Types;

namespace AbsoluteBotTests.Foundation;

[Trait("Category", "Unit")]
[Trait("Area", "Foundation")]
public class CommandPermissionCheckerTests
{
    [Fact]
    public void IsOfficialChannel_TextChatContext_ReturnsTrue()
    {
        var context = ChatContextFactory.CreateTextContext();
        var parsed = new ParsedCommand("!test", "", context, UserRole.Default);

        Assert.True(CommandPermissionChecker.IsOfficialChannel(parsed));
    }

    [Fact]
    public void IsOfficialChannel_TelegramPremium_ReturnsTrue()
    {
        var context = CreateTelegramContext(ChannelType.Premium);
        var parsed = new ParsedCommand("!test", "", context, UserRole.Default);

        Assert.True(CommandPermissionChecker.IsOfficialChannel(parsed));
    }

    [Fact]
    public void IsOfficialChannel_TelegramGeneral_ReturnsFalse()
    {
        var context = CreateTelegramContext(ChannelType.General);
        var parsed = new ParsedCommand("!test", "", context, UserRole.Default);

        Assert.False(CommandPermissionChecker.IsOfficialChannel(parsed));
    }

    private static TelegramChatContext CreateTelegramContext(ChannelType channelType)
    {
        return new TelegramChatContext(
            "testuser",
            500,
            new FakeChatService(),
            channelId: 1,
            channelType,
            messageId: 1,
            new Message(),
            lastMessages: null,
            replyInfo: null);
    }
}
