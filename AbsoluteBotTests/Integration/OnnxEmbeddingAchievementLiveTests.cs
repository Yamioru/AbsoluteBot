using AbsoluteBot.Services.NeuralNetworkServices;
using AbsoluteBotTests.Infrastructure;

namespace AbsoluteBotTests.Integration;

/// <summary>
///     Живая проверка e5-small по каталогу ачивок. Нужна сеть (Hugging Face) при первом запуске.
///     Не входит в unit.runsettings.
/// </summary>
[Collection("BeginData")]
[Trait("Category", "Integration")]
[Trait("Area", "Services")]
public class OnnxEmbeddingAchievementLiveTests : IClassFixture<OnnxEmbeddingClassFixture>
{
    private readonly OnnxEmbeddingClassFixture _embed;

    public OnnxEmbeddingAchievementLiveTests(BeginDataFixture data, OnnxEmbeddingClassFixture embed)
    {
        _ = data;
        _embed = embed;
    }

    [Fact]
    public void Tokenizer_MatchesXlmrHelloWorld()
    {
        Assert.True(_embed.Classifier.IsReady, OnnxEmbeddingAchievementClassifier.DescribeNativeLoadEnvironment());
        Assert.Equal(new[] {35378, 8999}, _embed.Classifier.EncodeText("Hello world"));
    }

    [Fact]
    public void Model_Loads()
    {
        Assert.True(_embed.Classifier.IsReady,
            "e5-модель не загрузилась. " + OnnxEmbeddingAchievementClassifier.DescribeNativeLoadEnvironment());
        Assert.Equal(30, _embed.Catalog.Count);
    }

    [Fact]
    public void CatIsCloserThanShipper()
    {
        Assert.True(_embed.Classifier.IsReady, OnnxEmbeddingAchievementClassifier.DescribeNativeLoadEnvironment());
        var cat = _embed.Classifier.Cosine("котик", "котик, кошка, кот, котёнок");
        var ship = _embed.Classifier.Cosine("котик", "шип, OTP, пейринг, шипперят");
        Assert.True(cat > ship, $"котик: cat={cat:0.00} ship={ship:0.00}");
        Assert.True(cat >= EmbeddingAchievementScoring.DefaultMinCosine, $"котик слишком далеко от кошек: {cat:0.00}");
    }

    [Theory]
    [InlineData("котик", "cat_lover")]
    [InlineData("обожаю котиков", "cat_lover")]
    [InlineData("котики мои", "cat_lover")]
    [InlineData("сука проклятая", "sailor_mouth")]
    [InlineData("бля буду", "sailor_mouth")]
    [InlineData("мой пёс хороший", "doggo")]
    [InlineData("смотрю аниме", "weeb")]
    [InlineData("на улице дождь", "rain_man")]
    public async Task Classifies_ExpectedAchievement(string text, string expectedId)
    {
        Assert.True(_embed.Classifier.IsReady, OnnxEmbeddingAchievementClassifier.DescribeNativeLoadEnvironment());

        var scores = await _embed.Classifier.ScoreAllAsync(text, _embed.Catalog);
        var ids = await _embed.Classifier.ClassifyAsync(text, _embed.Catalog);

        Assert.True(
            ids.Contains(expectedId, StringComparer.OrdinalIgnoreCase),
            $"Ожидали {expectedId} для «{text}». ids=[{string.Join(", ", ids)}] top={FormatTop(scores.Scores)} decoy={scores.MaxDecoy:0.00}");
        Assert.True(
            ids.Count == 1,
            $"Для «{text}» должна быть одна ачивка, а не {ids.Count}: [{string.Join(", ", ids)}]");
    }

    [Theory]
    [InlineData("котик", "shipper")]
    [InlineData("ненавижу кошек", "cat_lover")]
    [InlineData("который", "cat_lover")]
    [InlineData("привет", "cat_lover")]
    [InlineData("привет", "sailor_mouth")]
    [InlineData("привет", "shipper")]
    [InlineData("Паша ты чорт", "cat_lover")]
    [InlineData("Паша ты чорт", "doggo")]
    [InlineData("Паша ты чорт", "horror_coward")]
    [InlineData("Паша ты чорт", "shipper")]
    [InlineData("Паша ты чорт", "sailor_mouth")]
    public async Task DoesNotClassify_WrongAchievement(string text, string forbiddenId)
    {
        Assert.True(_embed.Classifier.IsReady, OnnxEmbeddingAchievementClassifier.DescribeNativeLoadEnvironment());

        var ids = await _embed.Classifier.ClassifyAsync(text, _embed.Catalog);

        Assert.DoesNotContain(forbiddenId, ids, StringComparer.OrdinalIgnoreCase);
    }

    private static string FormatTop(IReadOnlyList<AbsoluteBot.Services.NeuralNetworkServices.AchievementScore> scores)
    {
        return string.Join("; ", scores
            .OrderByDescending(item => item.Cosine)
            .Take(5)
            .Select(item => $"{item.Id}:c={item.Cosine:0.00}/n={item.NegativeCosine:0.00}"));
    }
}
