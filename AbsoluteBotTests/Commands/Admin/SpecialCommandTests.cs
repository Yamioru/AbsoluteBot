using AbsoluteBot.Chat.Commands;
using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services.CommandManagementServices;
using AbsoluteBotTests.Infrastructure;
using Moq;

namespace AbsoluteBotTests.Commands.Admin;

[Trait("Category", "Unit")]
[Trait("Area", "Commands")]
public class SpecialCommandTests
{
    [Fact]
    public void Almighty_CanExecute_RequiresExclamationAndOfficial()
    {
        var gemini = CommandTestMocks.CreateAskGeminiMock();
        var cmd = new AlmightyCommand(gemini.Object);

        var ok = ChatContextFactory.CreateParsed("!сделай", "кофе", role: UserRole.Default);
        Assert.True(cmd.CanExecute(ok));

        var ignored = ChatContextFactory.CreateParsed("!сделай", role: UserRole.Ignored);
        Assert.False(cmd.CanExecute(ignored));

        var noBang = ChatContextFactory.CreateParsed("сделай", role: UserRole.Default);
        Assert.False(cmd.CanExecute(noBang));

        var generalTg = new ParsedCommand("!сделай", "", ChatContextFactory.CreateTelegramContext(ChannelType.General), UserRole.Default);
        Assert.False(cmd.CanExecute(generalTg));
    }

    [Fact]
    public async Task Almighty_ExecuteAsync_HappyPath()
    {
        var gemini = CommandTestMocks.CreateAskGeminiMock();
        gemini.Setup(g => g.AskAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<double>()))
            .ReturnsAsync("Готово!");
        var cmd = new AlmightyCommand(gemini.Object);
        var chat = new FakeChatService();
        var parsed = ChatContextFactory.CreateParsed("!сделай", "кофе", chat);

        var result = await cmd.ExecuteAsync(parsed);

        Assert.Equal("Готово!", result);
        Assert.Single(chat.SentMessages);
    }

    [Fact]
    public void ExecuteExtra_CanExecute_WhenCommandExists()
    {
        var extras = new Mock<ExtraCommandsService>();
        extras.Setup(e => e.GetCommand("!discord")).Returns("link");
        var cmd = new ExecuteExtraCommand(extras.Object);

        Assert.True(cmd.CanExecute(ChatContextFactory.CreateParsed("!discord")));
        Assert.False(cmd.CanExecute(ChatContextFactory.CreateParsed("!missing")));
        Assert.False(cmd.CanExecute(ChatContextFactory.CreateParsed("!discord", role: UserRole.Ignored)));
    }

    [Fact]
    public async Task ExecuteExtra_ExecuteAsync_HappyPath()
    {
        var extras = new Mock<ExtraCommandsService>();
        extras.Setup(e => e.GetCommand("!discord")).Returns("https://discord.gg/test");
        var cmd = new ExecuteExtraCommand(extras.Object);
        var chat = new FakeChatService();

        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed("!discord", chatService: chat));

        Assert.Equal("https://discord.gg/test", result);
        Assert.Single(chat.SentMessages);
    }
}
