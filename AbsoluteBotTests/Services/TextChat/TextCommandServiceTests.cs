using AbsoluteBot.Chat.Commands;
using AbsoluteBot.Chat.Commands.Registry;
using AbsoluteBot.Chat.Context;
using AbsoluteBot.Messaging;
using AbsoluteBot.Services.CommandManagementServices;
using AbsoluteBot.Services.TextChat;
using AbsoluteBot.Services.UserManagementServices;
using AbsoluteBotTests.Infrastructure;

namespace AbsoluteBotTests.Services.TextChat;

[Trait("Category", "Unit")]
[Trait("Area", "Services")]
public class InMemoryChatServiceTests
{
    [Fact]
    public async Task SendMessageAsync_TracksMessages()
    {
        var service = new InMemoryChatService();
        var context = ChatContextFactory.CreateTextContext(service);

        await service.SendMessageAsync("one", context);
        await service.SendMessageAsync("two", context);

        Assert.Equal(new[] { "one", "two" }, service.SentMessages);
        Assert.Equal("two", service.LastSentMessage);
    }

    [Fact]
    public async Task Connect_IsNoOp()
    {
        var service = new InMemoryChatService();
        await service.Connect();
        Assert.Null(service.LastSentMessage);
    }
}

[Collection("BeginData")]
[Trait("Category", "Unit")]
[Trait("Area", "Services")]
public class TextCommandServiceTests
{
    [Fact]
    public async Task ProcessAsync_MapsKeywordToCommand_AndPrefersLastSent()
    {
        var roleService = new RoleService();
        await roleService.InitializeAsync();

        var cooldownService = new CooldownService();
        await cooldownService.InitializeAsync();
        await cooldownService.SetCooldownAsync("WebText", "0");

        var statusService = new CommandStatusService();
        await statusService.InitializeAsync();
        await statusService.SetCommandStatusAsync("!hello", "WebText", true);

        var registry = new CommandRegistry();
        registry.RegisterCommand(new SendingStubCommand());

        var execution = new CommandExecutionService(
            new CommandParser(),
            registry,
            roleService,
            cooldownService,
            statusService);

        var textService = new TextCommandService(registry, execution);
        var result = await textService.ProcessAsync("hello", "tester");

        Assert.Equal("sent-via-chat", result);
    }

    [Fact]
    public async Task ProcessAsync_UnknownText_FallsBackToDefaultReply()
    {
        var roleService = new RoleService();
        await roleService.InitializeAsync();
        var cooldownService = new CooldownService();
        await cooldownService.InitializeAsync();
        await cooldownService.SetCooldownAsync("WebText", "0");
        var statusService = new CommandStatusService();
        await statusService.InitializeAsync();

        var registry = new CommandRegistry();
        var execution = new CommandExecutionService(
            new CommandParser(),
            registry,
            roleService,
            cooldownService,
            statusService);

        var textService = new TextCommandService(registry, execution);
        var result = await textService.ProcessAsync("something unknown", "tester");

        Assert.Equal("Я не знаю, что ответить", result);
    }

    private sealed class SendingStubCommand : IChatCommand
    {
        public int Priority => 0;
        public string Description => "stub";
        public string Name => "!hello";
        public bool CanExecute(ParsedCommand command) => true;

        public async Task<string> ExecuteAsync(ParsedCommand command)
        {
            await command.Context.ChatService.SendMessageAsync("sent-via-chat", command.Context);
            return "return-value";
        }
    }
}
