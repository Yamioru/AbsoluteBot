using AbsoluteBot.Services.NeuralNetworkServices;
using AbsoluteBotTests.Infrastructure;

namespace AbsoluteBotTests.Services.Neural;

[Collection("BeginData")]
[Trait("Category", "Unit")]
[Trait("Area", "Services")]
public class ChatHistoryTests(BeginDataFixture fixture)
{
    [Fact]
    public async Task AddMessage_AndGetHistory_ReplacesPlaceholders()
    {
        _ = fixture;
        var history = new ChatHistory();
        await history.AddInitialMessagesAsync("Hello {Date}", "Hi", "TestPlatform");

        var result = history.GetHistory(new Dictionary<string, string> { ["Date"] = "2026-07-22" });

        Assert.Equal(2, result.Count);
        Assert.Contains("2026-07-22", result[0]?["parts"]?[0]?["text"]?.ToString());
    }

    [Fact]
    public async Task IsMessageInRecentHistory_FindsAddedMessage()
    {
        _ = fixture;
        var history = new ChatHistory();
        await history.AddInitialMessagesAsync("u0", "m0", "P2");
        await history.AddMessageAsync("user", "unique-msg-42", "P2");

        Assert.True(history.IsMessageInRecentHistory("user", "unique-msg-42"));
        Assert.False(history.IsMessageInRecentHistory("user", "missing"));
    }

    [Fact]
    public async Task ClearExceptFirstTwo_KeepsSeedMessages()
    {
        _ = fixture;
        var history = new ChatHistory();
        await history.AddInitialMessagesAsync("u0", "m0", "P3");
        await history.AddMessageAsync("user", "extra1", "P3");
        await history.AddMessageAsync("model", "extra2", "P3");

        await history.ClearExceptFirstTwo();

        var result = history.GetHistory(new Dictionary<string, string>());
        Assert.Equal(2, result.Count);
    }
}
