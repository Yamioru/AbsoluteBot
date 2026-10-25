using AbsoluteBot.Chat.Commands;
using AbsoluteBot.Chat.Commands.Registry;
using AbsoluteBot.Chat.Context;
using AbsoluteBot.Messaging;
using AbsoluteBot.Models;
using AbsoluteBot.Services.CommandManagementServices;
using AbsoluteBot.Services.UserManagementServices;
using AbsoluteBotTests.Infrastructure;

namespace AbsoluteBotTests.Foundation;

[Collection("BeginData")]
[Trait("Category", "Unit")]
[Trait("Area", "Foundation")]
public class CommandExecutionServiceTests
{
    [Fact]
    public async Task ExecuteCommandAsync_HappyPath_RunsStubCommand()
    {
        var (service, chat) = await CreateSutAsync(enableCommand: true, cooldownSeconds: 0);
        var context = ChatContextFactory.CreateTextContext(chat);

        var result = await service.ExecuteCommandAsync("!stubcmd", context);

        Assert.Equal("stub-ok", result);
    }

    [Fact]
    public async Task ExecuteCommandAsync_OnCooldown_ReturnsCooldownMessage()
    {
        var (service, chat) = await CreateSutAsync(enableCommand: true, cooldownSeconds: 60);
        var context = ChatContextFactory.CreateTextContext(chat);

        // Первый вызов ставит last-used
        Assert.Equal("stub-ok", await service.ExecuteCommandAsync("!stubcmd", context));

        var result = await service.ExecuteCommandAsync("!stubcmd", context);

        Assert.Equal("Бот на перезарядке. Подождите немного.", result);
        Assert.Contains(chat.SentMessages, m => m.Message.Contains("перезарядке"));
    }

    [Fact]
    public async Task ExecuteCommandAsync_DisabledCommand_ReturnsNull()
    {
        var (service, chat) = await CreateSutAsync(enableCommand: false, cooldownSeconds: 0);
        var context = ChatContextFactory.CreateTextContext(chat);

        var result = await service.ExecuteCommandAsync("!stubcmd", context);

        Assert.Null(result);
    }

    private static async Task<(CommandExecutionService Service, FakeChatService Chat)> CreateSutAsync(
        bool enableCommand, int cooldownSeconds)
    {
        var roleService = new RoleService();
        await roleService.InitializeAsync();

        var cooldownService = new CooldownService();
        await cooldownService.InitializeAsync();
        await cooldownService.SetCooldownAsync("WebText", cooldownSeconds.ToString());

        var statusService = new CommandStatusService();
        await statusService.InitializeAsync();
        await statusService.SetCommandStatusAsync("!stubcmd", "WebText", enableCommand);

        var registry = new CommandRegistry();
        registry.RegisterCommand(new StubCommand());

        var execution = new CommandExecutionService(
            new CommandParser(),
            registry,
            roleService,
            cooldownService,
            statusService);

        return (execution, new FakeChatService());
    }

    private sealed class StubCommand : IChatCommand
    {
        public int Priority => 0;
        public string Description => "stub";
        public string Name => "!stubcmd";
        public bool CanExecute(ParsedCommand command) => true;
        public Task<string> ExecuteAsync(ParsedCommand command) => Task.FromResult("stub-ok");
    }
}
