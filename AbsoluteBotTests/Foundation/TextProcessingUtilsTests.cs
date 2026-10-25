using AbsoluteBot.Helpers;

namespace AbsoluteBotTests.Foundation;

[Trait("Category", "Unit")]
[Trait("Area", "Foundation")]
public class TextProcessingUtilsTests
{
    [Fact]
    public void CleanHtmlTags_RemovesTagsAndDecodesEntities()
    {
        var result = TextProcessingUtils.CleanHtmlTags("<b>привет&amp;</b>");
        Assert.Equal("привет&", result);
    }

    [Fact]
    public void CleanHtmlTags_Empty_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, TextProcessingUtils.CleanHtmlTags("   "));
    }

    [Fact]
    public void CutSentence_ShortText_Unchanged()
    {
        Assert.Equal("короткий", TextProcessingUtils.CutSentence("короткий", 100));
    }

    [Fact]
    public void CutSentence_LongText_Truncates()
    {
        var text = new string('a', 50) + ". " + new string('b', 50);
        var result = TextProcessingUtils.CutSentence(text, 40);
        Assert.True(result.Length <= 40 || result.EndsWith('.'));
    }
}
