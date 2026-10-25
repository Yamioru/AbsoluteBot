using AbsoluteBot.Messaging;
using AbsoluteBot.Models;
using AbsoluteBotTests.Infrastructure;

namespace AbsoluteBotTests.Foundation;

[Trait("Category", "Unit")]
[Trait("Area", "Foundation")]
public class CommandParserTests
{
    private readonly CommandParser _parser = new();

    [Theory]
    [InlineData("!факт", "!факт", "")]
    [InlineData("!переведи hello", "!переведи", "hello")]
    [InlineData("!!спросить что", "!!спросить", "что")]
    [InlineData("@bot hi", "@bot", "hi")]
    public void Parse_ExtractsCommandAndParameters(string message, string expectedCommand, string expectedParams)
    {
        var context = ChatContextFactory.CreateTextContext();
        var parsed = _parser.Parse(message, context, UserRole.Default);
        Assert.NotNull(parsed);
        Assert.Equal(expectedCommand, parsed!.Command);
        Assert.Equal(expectedParams, parsed.Parameters);
    }

    [Theory]
    [InlineData("")]
    [InlineData("просто текст")]
    [InlineData("факт без префикса")]
    public void Parse_NonCommand_ReturnsNull(string message)
    {
        var context = ChatContextFactory.CreateTextContext();
        Assert.Null(_parser.Parse(message, context, UserRole.Default));
    }
}
