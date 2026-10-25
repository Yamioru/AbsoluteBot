using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services.ChatServices;
using AbsoluteBot.Services.UserManagementServices;
using AbsoluteBot.Services.UtilityServices;
using AbsoluteBotTests.Infrastructure;
using Moq;

namespace AbsoluteBotTests.Handlers;

[Trait("Category", "Unit")]
[Trait("Area", "Handlers")]
public class BaseMessageHandlerTests
{
    private sealed class TestHandler(ConfigService config, RoleService roles) : BaseMessageHandler(config, roles)
    {
        public bool Mentioned { get; set; }

        protected override bool IsBotMentioned(string text, ChatContext context) => Mentioned;

        public string CallAddBotMention(string text) => AddBotMention(text);
        public void CallSaveLastMessage(string user, string text) => SaveLastMessage(user, text);
        public bool CallShouldRandomlyMention(double p) => ShouldRandomlyMentionBot(p);
        public Task<string?> CallHandleMention(string text, ChatContext ctx, double p = 0) =>
            HandleMention(text, ctx, p);
        public int LastMessageCount => LastMessages.Count;
    }

    private static async Task<TestHandler> CreateHandlerAsync(string botName = "Sobeka")
    {
        var config = new Mock<ConfigService>();
        config.Setup(c => c.GetConfigValueAsync<string>("BotName")).ReturnsAsync(botName);
        var roles = new Mock<RoleService>();
        roles.Setup(r => r.GetUserRoleAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>())).ReturnsAsync(UserRole.Default);
        var handler = new TestHandler(config.Object, roles.Object);
        await handler.InitializeAsync();
        return handler;
    }

    [Fact]
    public async Task IsMessageInvalid_NullOrBadPattern_IsInvalid()
    {
        var handler = await CreateHandlerAsync();
        Assert.True(handler.IsMessageInvalid(null));
        Assert.True(handler.IsMessageInvalid("no colon here"));
        Assert.False(handler.IsMessageInvalid("alice: hi"));
        Assert.False(handler.IsMessageInvalid("[Twitch] raw"));
    }

    [Fact]
    public async Task AddBotMention_PrependsAtBotName()
    {
        var handler = await CreateHandlerAsync("Sobeka");
        Assert.Equal("@Sobeka hello", handler.CallAddBotMention("hello"));
        Assert.Equal("@Sobeka already", handler.CallAddBotMention("@Sobeka already"));
        Assert.Equal("!cmd", handler.CallAddBotMention("!cmd"));
    }

    [Fact]
    public async Task SaveLastMessage_CapsQueueAtTen()
    {
        var handler = await CreateHandlerAsync();
        for (var i = 0; i < 15; i++)
            handler.CallSaveLastMessage("u", $"m{i}");

        Assert.Equal(10, handler.LastMessageCount);
    }

    [Fact]
    public async Task HandleMention_ReturnsNull_ForIgnoredUser()
    {
        var config = new Mock<ConfigService>();
        config.Setup(c => c.GetConfigValueAsync<string>("BotName")).ReturnsAsync("Sobeka");
        var roles = new Mock<RoleService>();
        roles.Setup(r => r.GetUserRoleAsync("ignored", It.IsAny<string?>(), It.IsAny<string?>())).ReturnsAsync(UserRole.Ignored);
        var handler = new TestHandler(config.Object, roles.Object);
        await handler.InitializeAsync();

        var ctx = ChatContextFactory.CreateTextContext(username: "ignored");
        var result = await handler.CallHandleMention("hi", ctx);

        Assert.Null(result);
    }

    [Fact]
    public async Task HandleMention_AddsMention_WhenBotMentionedAndReplyInvalid()
    {
        var handler = await CreateHandlerAsync();
        handler.Mentioned = true;
        var ctx = ChatContextFactory.CreateTextContext();
        // Reply null → IsMessageInvalid(null) → true
        ctx.Reply = null;

        var result = await handler.CallHandleMention("question", ctx);

        Assert.Equal("@Sobeka question", result);
    }
}
