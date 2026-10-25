using AbsoluteBot.Chat.Commands.Registry;
using AbsoluteBot.Chat.Commands.UserCommands;
using AbsoluteBot.Chat.Context;
using AbsoluteBot.Messaging;
using AbsoluteBot.Models;
using AbsoluteBot.Services.CommandManagementServices;
using AbsoluteBot.Services.NeuralNetworkServices;
using AbsoluteBot.Services.UserManagementServices;
using AbsoluteBot.Services.UtilityServices;
using AbsoluteBotTests.Infrastructure;
using Moq;

namespace AbsoluteBotTests.Commands.User;

[Collection("BeginData")]
[Trait("Category", "Unit")]
[Trait("Area", "Commands")]
public class MentionCommandTests(BeginDataFixture fixture)
{
    [Fact]
    public void CanExecute_OfficialNotIgnoredOrBot()
    {
        var cmd = CreateMention();
        Assert.True(cmd.CanExecute(ChatContextFactory.CreateParsed("@bot")));
        Assert.False(cmd.CanExecute(ChatContextFactory.CreateParsed("@bot", role: UserRole.Ignored)));
        Assert.False(cmd.CanExecute(ChatContextFactory.CreateParsed("@bot", role: UserRole.Bot)));
    }

    [Fact]
    public async Task ExecuteAsync_HappyPath()
    {
        _ = fixture;
        var gemini = CommandTestMocks.CreateChatGeminiMock();
        gemini.Setup(g => g.ChatAsync(It.IsAny<string>(), It.IsAny<ReplyInfo?>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync("Привет!");

        var config = new ConfigService();
        await config.InitializeAsync();
        var cmd = CreateMention(gemini, config);
        await cmd.InitializeAsync();

        var chat = new FakeChatService();
        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name, "как дела", chat));

        Assert.Equal("Привет!", result);
        Assert.Single(chat.SentMessages);
    }

    private static MentionCommand CreateMention(Mock<INeuralChatService>? gemini = null, ConfigService? config = null)
    {
        gemini ??= CommandTestMocks.CreateChatGeminiMock();
        var execution = new Mock<CommandExecutionService>(
            Mock.Of<ICommandParser>(),
            Mock.Of<ICommandRegistry>(),
            new Mock<RoleService>().Object,
            new Mock<CooldownService>().Object,
            new Mock<CommandStatusService>().Object);
        execution.Setup(e => e.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<ChatContext>())).ReturnsAsync((string?)null);
        return new MentionCommand(gemini.Object, execution.Object, config ?? new ConfigService());
    }
}
