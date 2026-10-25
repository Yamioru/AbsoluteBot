using AbsoluteBot.Chat.Commands;
using AbsoluteBot.Chat.Commands.Registry;
using AbsoluteBot.Chat.Context;

namespace AbsoluteBotTests.Foundation;

[Trait("Category", "Unit")]
[Trait("Area", "Foundation")]
public class CommandRegistryTests
{
    [Fact]
    public void RegisterAndFindCommand_ExactName()
    {
        var registry = new CommandRegistry();
        var command = new StubCommand("!ping");
        registry.RegisterCommand(command);

        var found = registry.FindCommand("!ping");
        Assert.Same(command, found);
    }

    [Fact]
    public void FindCommand_IsCaseInsensitive()
    {
        var registry = new CommandRegistry();
        registry.RegisterCommand(new StubCommand("!Ping"));

        Assert.NotNull(registry.FindCommand("!PING"));
    }

    [Fact]
    public void FindCommand_Unknown_ReturnsNull()
    {
        var registry = new CommandRegistry();
        registry.RegisterCommand(new StubCommand("!ping"));

        Assert.Null(registry.FindCommand("!zzzzzzzzzzzz_unknown"));
    }

    [Fact]
    public void GetAllCommands_ReturnsRegistered()
    {
        var registry = new CommandRegistry();
        registry.RegisterCommand(new StubCommand("!a"));
        registry.RegisterCommand(new StubCommand("!b"));

        Assert.Equal(2, registry.GetAllCommands().Count());
    }

    private sealed class StubCommand(string name) : IChatCommand
    {
        public int Priority => 0;
        public string Description => "stub";
        public string Name { get; } = name;
        public bool CanExecute(ParsedCommand command) => true;
        public Task<string> ExecuteAsync(ParsedCommand command) => Task.FromResult("ok");
    }
}
