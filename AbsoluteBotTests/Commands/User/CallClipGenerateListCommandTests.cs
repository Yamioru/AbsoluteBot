using System.Runtime.CompilerServices;
using AbsoluteBot.Chat.Commands.Registry;
using AbsoluteBot.Chat.Commands.UserCommands;
using AbsoluteBot.Chat.Context;
using AbsoluteBot.Models;
using AbsoluteBot.Services;
using AbsoluteBot.Services.ChatServices.Discord;
using AbsoluteBot.Services.ChatServices.TwitchChat;
using AbsoluteBot.Services.CommandManagementServices;
using AbsoluteBot.Services.MediaServices;
using AbsoluteBot.Services.NeuralNetworkServices;
using AbsoluteBot.Services.UtilityServices;
using AbsoluteBotTests.Infrastructure;
using Moq;

namespace AbsoluteBotTests.Commands.User;

[Trait("Category", "Unit")]
[Trait("Area", "Commands")]
public class CallClipGenerateListCommandTests
{
    [Fact]
    public void Call_CanExecute_StreamingOnly()
    {
        var cmd = new CallCommand(Uninitialized<DiscordChatService>());
        Assert.True(cmd.CanExecute(ChatContextFactory.CreateStreamingParsed(cmd.Name)));
        Assert.False(cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name)));
        Assert.False(cmd.CanExecute(ChatContextFactory.CreateStreamingParsed(cmd.Name, role: UserRole.Ignored)));
    }

    [Fact]
    public async Task Call_MissingParameters_ReturnsUsage()
    {
        var cmd = new CallCommand(Uninitialized<DiscordChatService>());
        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateStreamingParsed(cmd.Name, ""));
        Assert.Contains("Использование", result);
    }

    [Fact]
    public void Clip_CanExecute_OfficialNotIgnored()
    {
        var cmd = new ClipCommand(
            Uninitialized<TwitchChatService>(),
            CommandTestMocks.CreateAskGeminiMock().Object,
            new Mock<ClipsService>().Object);
        Assert.True(cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name)));
        Assert.False(cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name, role: UserRole.Ignored)));
        Assert.False(cmd.CanExecute(new ParsedCommand(cmd.Name, "", ChatContextFactory.CreateTelegramContext(ChannelType.General), UserRole.Default)));
    }

    [Fact]
    public void ImageGenerate_CanExecute_AdminOrPremium()
    {
        var gen = new Mock<ImageGenerationService>(new ConfigService(), new HttpClient());
        var cmd = new ImageGenerateCommand(gen.Object, CommandTestMocks.CreateTranslationMock().Object);
        Assert.True(cmd.CanExecute(ChatContextFactory.CreateAdminChannelParsed(cmd.Name)));
        Assert.False(cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name)));
    }

    [Fact]
    public async Task ImageGenerate_MissingParameters_ReturnsUsage()
    {
        var gen = new Mock<ImageGenerationService>(new ConfigService(), new HttpClient());
        var cmd = new ImageGenerateCommand(gen.Object, CommandTestMocks.CreateTranslationMock().Object);
        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateAdminChannelParsed(cmd.Name, ""));
        Assert.Contains("Использование", result);
    }

    [Fact]
    public void GeminiImageGenerate_CanExecute_AdminOrPremium()
    {
        var gen = new Mock<CloudflareFluxImageService>(new ConfigService(), new HttpClient());
        var cmd = new GeminiImageGenerateCommand(gen.Object, CommandTestMocks.CreateTranslationMock().Object);
        Assert.True(cmd.CanExecute(ChatContextFactory.CreateAdminChannelParsed(cmd.Name)));
        Assert.False(cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name)));
    }

    [Fact]
    public async Task GeminiImageGenerate_MissingParameters_ReturnsUsage()
    {
        var gen = new Mock<CloudflareFluxImageService>(new ConfigService(), new HttpClient());
        var cmd = new GeminiImageGenerateCommand(gen.Object, CommandTestMocks.CreateTranslationMock().Object);
        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateAdminChannelParsed(cmd.Name, ""));
        Assert.Contains("Использование", result);
    }

    [Fact]
    public void CommandsList_CanExecute_AlwaysTrue()
    {
        var cmd = new CommandsListCommand(CreateCommandsListMock().Object);
        Assert.True(cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name)));
        Assert.True(cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name, role: UserRole.Ignored)));
    }

    [Fact]
    public async Task CommandsList_HappyPath()
    {
        var list = CreateCommandsListMock();
        list.Setup(l => l.GenerateCommandsListAsImageAsync(It.IsAny<ParsedCommand>())).ReturnsAsync("https://list.png");
        var chat = new FakeChatService();
        var cmd = new CommandsListCommand(list.Object);

        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name, chatService: chat));

        Assert.Equal("https://list.png", result);
        Assert.Single(chat.SentMessages);
    }

    private static Mock<CommandsListService> CreateCommandsListMock() =>
        new(Mock.Of<ICommandRegistry>(), Mock.Of<IImageGeneratorService>(), new Mock<ExtraCommandsService>().Object);

    private static T Uninitialized<T>() where T : class =>
        (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
}
