using AbsoluteBot.Helpers;
using AbsoluteBot.Services.ScheduledTasks;

namespace AbsoluteBotTests.Foundation;

[Trait("Category", "Unit")]
[Trait("Area", "Foundation")]
public class TelegramHtmlFormatterTests
{
    [Fact]
    public void ToHtml_ConvertsBoldAndHeading()
    {
        var html = TelegramHtmlFormatter.ToHtml("### Заголовок\n\nЛинус хотел назвать ОС **Freak**.", 500);

        Assert.Contains("<b>Заголовок</b>", html);
        Assert.Contains("<b>Freak</b>", html);
        Assert.DoesNotContain("###", html);
        Assert.DoesNotContain("**", html);
    }

    [Fact]
    public void TruncateHtml_ClosesOpenTags()
    {
        var html = TelegramHtmlFormatter.ToHtml("**" + new string('а', 80) + "**", 20);

        Assert.StartsWith("<b>", html);
        Assert.EndsWith("</b>", html);
        Assert.True(html.Length < 80);
    }

    [Fact]
    public void Compose_PutsTitleInBoldAndEscapesHtml()
    {
        var html = TelegramHtmlFormatter.Compose("Сегодня: Linux <beta>", "Факт про **Freak**.", 200);

        Assert.Contains("<b>Сегодня: Linux &lt;beta&gt;</b>", html);
        Assert.Contains("<b>Freak</b>", html);
    }

    [Fact]
    public void HasIntactMarkup_TrueForBoldAndHeading()
    {
        Assert.True(TelegramHtmlFormatter.HasIntactMarkup("Он хотел назвать ОС **Freak**."));
        Assert.True(TelegramHtmlFormatter.HasIntactMarkup("### Заголовок\nтекст"));
        Assert.True(TelegramHtmlFormatter.HasIntactMarkup("*!команды* — список"));
    }

    [Fact]
    public void HasIntactMarkup_FalseWhenBrokenOrPlain()
    {
        Assert.False(TelegramHtmlFormatter.HasIntactMarkup("просто текст без разметки"));
        Assert.False(TelegramHtmlFormatter.HasIntactMarkup("сломано **жирный без закрытия"));
        Assert.False(TelegramHtmlFormatter.HasIntactMarkup("код `без закрытия"));
    }

    [Fact]
    public void TryFormat_ConvertsTelegramLegacyAndSkipsBroken()
    {
        Assert.True(TelegramHtmlFormatter.TryFormat("*!команды* _вопрос_ — описание", 500, out var html));
        Assert.Contains("<b>!команды</b>", html);
        Assert.Contains("<i>вопрос</i>", html);

        Assert.False(TelegramHtmlFormatter.TryFormat("привет **мир", 500, out _));
    }

    [Fact]
    public void ToHtml_ConvertsLink()
    {
        var html = TelegramHtmlFormatter.ToHtml("смотри [сайт](https://example.com)", 200);
        Assert.Contains("<a href=\"https://example.com\">сайт</a>", html);
    }

    [Fact]
    public void BuildHolidayHtml_CutsLongFactAndKeepsTitle()
    {
        var longFact = string.Join(' ', Enumerable.Repeat("слово", 200));
        var html = TelegramTasksService.BuildHolidayHtml("День Linux", longFact);

        Assert.Contains("<b>Сегодня праздник: День Linux</b>", html);
        Assert.True(html.Length <= TelegramTasksService.MaxPostLength + 80);
        Assert.DoesNotContain("**", html);
    }
}
