using AbsoluteBot.Models;
using AbsoluteBot.Services.NeuralNetworkServices;

namespace AbsoluteBotTests.Services.Neural;

[Trait("Category", "Unit")]
[Trait("Area", "Services")]
public class OnnxEmbeddingAchievementClassifierTests
{
    [Fact]
    public void ToLabel_UsesCatalogHypothesis()
    {
        var item = new AchievementDefinition
        {
            Id = "cat_lover", Name = "x", Goal = 1, Criteria = "Позитив про кошек.", NliHypothesis = "котик, кошка"
        };
        Assert.Equal("котик, кошка", EmbeddingAchievementScoring.ToLabel(item));
    }

    [Fact]
    public async Task Classify_WhenModelNotLoaded_ReturnsEmpty()
    {
        using var classifier = new OnnxEmbeddingAchievementClassifier(new HttpClient());
        var catalog = new AchievementDefinition[]
        {
            new() {Id = "cat_lover", Name = "x", Goal = 10, Criteria = "кошки"}
        };

        var ids = await classifier.ClassifyAsync("котик", catalog);

        Assert.False(classifier.IsReady);
        Assert.Empty(ids);
    }
}
