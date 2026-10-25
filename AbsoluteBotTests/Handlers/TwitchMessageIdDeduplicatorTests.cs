using AbsoluteBot.Services.ChatServices.TwitchChat;

namespace AbsoluteBotTests.Handlers;

[Trait("Category", "Unit")]
[Trait("Area", "Handlers")]
public class TwitchMessageIdDeduplicatorTests
{
    [Fact]
    public void TryTake_ReturnsFalse_ForDuplicateId()
    {
        var deduplicator = new TwitchMessageIdDeduplicator();

        Assert.True(deduplicator.TryTake("msg-1"));
        Assert.False(deduplicator.TryTake("msg-1"));
        Assert.True(deduplicator.TryTake("msg-2"));
    }

    [Fact]
    public void TryTake_AllowsEmptyIds()
    {
        var deduplicator = new TwitchMessageIdDeduplicator();

        Assert.True(deduplicator.TryTake(null));
        Assert.True(deduplicator.TryTake(null));
        Assert.True(deduplicator.TryTake(""));
        Assert.True(deduplicator.TryTake(""));
    }

    [Fact]
    public void TryTake_EvictsOldest_WhenOverCapacity()
    {
        var deduplicator = new TwitchMessageIdDeduplicator(2);

        Assert.True(deduplicator.TryTake("a"));
        Assert.True(deduplicator.TryTake("b"));
        Assert.True(deduplicator.TryTake("c"));
        Assert.True(deduplicator.TryTake("a"));
        Assert.False(deduplicator.TryTake("c"));
    }
}
