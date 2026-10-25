using AbsoluteBot.Services.MediaServices;
using AbsoluteBot.Services.Recap;
using AbsoluteBot.Services.UtilityServices;
using Moq;

namespace AbsoluteBotTests.Services;

[Trait("Category", "Unit")]
[Trait("Area", "Services")]
public class RecapQueryParserTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 15, 0, 0, TimeSpan.FromHours(1));

    [Theory]
    [InlineData("вчера", -1)]
    [InlineData("за вчера", -1)]
    [InlineData("на стриме вчера", -1)]
    [InlineData("позавчера", -2)]
    [InlineData("3 дня назад", -3)]
    [InlineData("три дня назад", -3)]
    [InlineData("в пятницу", -2)]
    [InlineData("в эту пятницу", -2)]
    [InlineData("в прошлую пятницу", -2)]
    public void CalendarDays(string tail, int dayOffset)
    {
        var parsed = RecapQueryParser.Parse(tail, RecapPlace.StreamChat, Now);
        var day = Assert.IsType<CalendarDayWindow>(parsed.Window);
        Assert.Equal(DateOnly.FromDateTime(Now.DateTime).AddDays(dayOffset), day.Day);
        Assert.Equal(RecapSource.Stream, parsed.Source);
    }

    [Fact]
    public void Empty_OnStream_IsCurrentStream()
    {
        var parsed = RecapQueryParser.Parse(null, RecapPlace.StreamChat, Now);
        Assert.True(parsed.Ok);
        Assert.Equal(RecapSource.Stream, parsed.Source);
        Assert.IsType<CurrentStreamWindow>(parsed.Window);
    }

    [Fact]
    public void Empty_OnTelegram_IsSinceAuthor()
    {
        var parsed = RecapQueryParser.Parse("  ", RecapPlace.Telegram, Now);
        Assert.Equal(RecapSource.Telegram, parsed.Source);
        Assert.IsType<SinceAuthorWindow>(parsed.Window);
    }

    [Fact]
    public void SinceYesterday_IsRangeNotOneDay()
    {
        var parsed = RecapQueryParser.Parse("со вчера", RecapPlace.Telegram, Now);
        var range = Assert.IsType<RangeWindow>(parsed.Window);
        Assert.Equal(new DateOnly(2026, 10, 24), DateOnly.FromDateTime(range.From.DateTime));
        Assert.Equal(Now, range.To);
    }

    [Theory]
    [InlineData("на прошлом", 1)]
    [InlineData("прошлый стрим", 1)]
    [InlineData("на стриме прошлом", 1)]
    [InlineData("позапрошлый", 2)]
    [InlineData("2 стрима назад", 2)]
    [InlineData("два стрима назад", 2)]
    public void StreamOffsets(string tail, int steps)
    {
        var parsed = RecapQueryParser.Parse(tail, RecapPlace.StreamChat, Now);
        var window = Assert.IsType<StreamBackWindow>(parsed.Window);
        Assert.Equal(steps, window.Steps);
    }

    [Theory]
    [InlineData("на стриме")]
    [InlineData("этот стрим")]
    [InlineData("сейчас")]
    public void StreamWords_OnTelegram_OpenCurrentStream(string tail)
    {
        var parsed = RecapQueryParser.Parse(tail, RecapPlace.Telegram, Now);
        Assert.Equal(RecapSource.Stream, parsed.Source);
        Assert.IsType<CurrentStreamWindow>(parsed.Window);
    }

    [Fact]
    public void TelegramWords_FromTwitch_KeepTelegramSource()
    {
        var parsed = RecapQueryParser.Parse("в телеграме вчера", RecapPlace.StreamChat, Now);
        Assert.Equal(RecapSource.Telegram, parsed.Source);
        Assert.IsType<CalendarDayWindow>(parsed.Window);
    }

    [Fact]
    public void UnknownTail_IsHint()
    {
        var parsed = RecapQueryParser.Parse("абракадабра", RecapPlace.StreamChat, Now);
        Assert.False(parsed.Ok);
        Assert.Equal(RecapQueryParser.Hint, parsed.Error);
    }

    [Fact]
    public void WeekRange_IsSevenDays()
    {
        var parsed = RecapQueryParser.Parse("за неделю", RecapPlace.Discord, Now);
        var range = Assert.IsType<RangeWindow>(parsed.Window);
        Assert.Equal(TimeSpan.FromDays(7), range.To - range.From);
        Assert.Equal(RecapSource.Discord, parsed.Source);
    }
}

[Trait("Category", "Unit")]
[Trait("Area", "Services")]
public class NickMaskTests
{
    [Fact]
    public void SameNick_GetsOneLabel_LongerNickFirst()
    {
        var masked = NickMask.Apply("Кот и Котёнок написали. Кот снова.", new[] {"Кот", "Котёнок"});
        Assert.Contains("User_1", masked);
        Assert.Contains("User_2", masked);
        Assert.DoesNotContain("Котёнок", masked);
        Assert.Equal("зритель молчал", NickMask.StripLabels("User_2 молчал"));
    }
}

[Trait("Category", "Unit")]
[Trait("Area", "Services")]
public class TwitchPlaylistTests
{
    [Fact]
    public void LastSegment_SkipsHeaders()
    {
        const string playlist = """
                                #EXTM3U
                                #EXTINF:2.0,
                                https://cdn.example/1.ts
                                #EXTINF:2.0,
                                https://cdn.example/2.ts
                                """;

        Assert.Equal("https://cdn.example/2.ts", TwitchFrameGrabber.LastSegment(playlist));
    }

    [Fact]
    public void PickVariant_PrefersHeightNear720()
    {
        const string playlist = """
                                #EXTM3U
                                #EXT-X-STREAM-INF:BANDWIDTH=1000,RESOLUTION=1920x1080
                                https://cdn.example/1080.m3u8
                                #EXT-X-STREAM-INF:BANDWIDTH=800,RESOLUTION=1280x720
                                https://cdn.example/720.m3u8
                                #EXT-X-STREAM-INF:BANDWIDTH=1,RESOLUTION=256x144,VIDEO="audio_only"
                                https://cdn.example/audio.m3u8
                                """;

        Assert.Equal("https://cdn.example/720.m3u8", TwitchFrameGrabber.PickVariant(playlist));
    }
}

[Trait("Category", "Unit")]
[Trait("Area", "Services")]
public class ChatJournalTests
{
    [Fact]
    public async Task Record_KeepsNick_SkipsTwitchRelay_AndScrubsMail()
    {
        var censor = new Mock<ICensorshipService>();
        censor.Setup(c => c.ApplyCensorship(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>()))
            .Returns((string text, int _, bool _) => text);
        var journal = new ChatJournalService(null!, censor.Object);
        var root = Path.Combine(Path.GetTempPath(), "absolutebot-journal-" + Guid.NewGuid().ToString("N"));
        journal.UseDirectory(root);
        try
        {
            journal.Record(RecapSource.Stream, "Зритель", "42", "привет a@b.co");
            journal.Record(RecapSource.Stream, "Бот", "1", "[Twitch] Зритель: привет");
            var rows = await journal.ReadAsync(RecapSource.Stream, DateTime.UnixEpoch, DateTime.UtcNow.AddMinutes(1), null);
            var row = Assert.Single(rows);
            Assert.Equal("Зритель", row.Nick);
            Assert.Contains("[почта]", row.Text);
            Assert.DoesNotContain("a@b.co", row.Text);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}