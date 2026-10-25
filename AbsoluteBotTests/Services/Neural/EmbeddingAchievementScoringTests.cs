using AbsoluteBot.Models;
using AbsoluteBot.Services.NeuralNetworkServices;

namespace AbsoluteBotTests.Services.Neural;

[Trait("Category", "Unit")]
[Trait("Area", "Services")]
public class EmbeddingAchievementScoringTests
{
    [Fact]
    public void ToLabel_UsesHypothesis()
    {
        var item = new AchievementDefinition
        {
            Id = "cat_lover", Name = "x", Goal = 1, Criteria = "Позитив про кошек.", NliHypothesis = "котик, кошка"
        };
        Assert.Equal("котик, кошка", EmbeddingAchievementScoring.ToLabel(item));
    }

    [Fact]
    public void SelectIds_RequiresCosineAboveThresholdAndAboveNegative()
    {
        var scores = new AchievementScore[]
        {
            new("cat_lover", 0.86f, 0.40f),
            new("shipper", 0.81f, 0.84f),
            new("weeb", 0.50f, 0f),
            new("rain_man", 0.82f, 0f)
        };

        Assert.Equal(["cat_lover"], EmbeddingAchievementScoring.SelectIds(scores));
    }

    [Fact]
    public void SelectIds_RejectsFlatGreetings()
    {
        var scores = new AchievementScore[]
        {
            new("cat_lover", 0.81f, 0f),
            new("shipper", 0.805f, 0f),
            new("weeb", 0.80f, 0f),
            new("rain_man", 0.802f, 0f)
        };

        Assert.Empty(EmbeddingAchievementScoring.SelectIds(scores));
    }

    [Fact]
    public void SelectIds_RejectsWhenSecondIsClose()
    {
        var scores = new AchievementScore[]
        {
            new("cat_lover", 0.88f, 0f),
            new("shipper", 0.87f, 0f),
            new("weeb", 0.70f, 0f)
        };

        Assert.Empty(EmbeddingAchievementScoring.SelectIds(scores));
    }

    [Theory]
    [InlineData("котик", "котик, кошка, кот, котёнок", true)]
    [InlineData("котики мои", "котик, кошка", true)]
    [InlineData("сука проклятая", "мат, бля, сука, хуй", true)]
    [InlineData("который", "котик, кошка, кот", false)]
    [InlineData("Паша ты чорт", "котик, кошка, кот", false)]
    [InlineData("Паша ты чорт", "боюсь хоррор", false)]
    [InlineData("привет", "котик, кошка", false)]
    public void MentionsLabel_UsesWholeWords(string text, string label, bool expected)
    {
        Assert.Equal(expected, EmbeddingAchievementScoring.MentionsLabel(text, label));
    }

    [Fact]
    public void Cosine_IdenticalNormalizedVectors_IsOne()
    {
        var vector = EmbeddingAchievementScoring.L2Normalize([3f, 4f]);
        Assert.Equal(1f, EmbeddingAchievementScoring.Cosine(vector, vector), 5);
    }

    [Fact]
    public void MeanPool_AveragesUnmaskedTokens()
    {
        var hidden = new float[] {2f, 0f, 4f, 0f, 9f, 9f};
        var mask = new long[] {1, 1, 0};
        var pooled = EmbeddingAchievementScoring.MeanPool(hidden, mask, 0, 3, 2);
        Assert.Equal(2, pooled.Length);
        Assert.True(pooled[0] > pooled[1]);
    }

    [Fact]
    public void EncodeSingle_WritesBosSepAndPadding()
    {
        var ids = new long[XlmrPairEncoder.MaxLength];
        var mask = new long[XlmrPairEncoder.MaxLength];
        XlmrPairEncoder.EncodeSingle([10, 11], ids, mask, 0, maxLength: 8);

        Assert.Equal(XlmrPairEncoder.BosId, ids[0]);
        Assert.Equal(10, ids[1]);
        Assert.Equal(11, ids[2]);
        Assert.Equal(XlmrPairEncoder.EosId, ids[3]);
        Assert.Equal(XlmrPairEncoder.PadId, ids[4]);
        Assert.Equal(1, mask[3]);
        Assert.Equal(0, mask[4]);
    }

    [Fact]
    public void EncodePair_WritesBosSepAndPadding()
    {
        var ids = new long[XlmrPairEncoder.MaxLength];
        var mask = new long[XlmrPairEncoder.MaxLength];
        XlmrPairEncoder.EncodePair([10, 11], [20], ids, mask, 0, maxLength: 16);

        Assert.Equal(XlmrPairEncoder.BosId, ids[0]);
        Assert.Equal(10, ids[1]);
        Assert.Equal(11, ids[2]);
        Assert.Equal(XlmrPairEncoder.EosId, ids[3]);
        Assert.Equal(XlmrPairEncoder.EosId, ids[4]);
        Assert.Equal(20, ids[5]);
        Assert.Equal(XlmrPairEncoder.EosId, ids[6]);
        Assert.Equal(XlmrPairEncoder.PadId, ids[7]);
        Assert.Equal(1, mask[6]);
        Assert.Equal(0, mask[7]);
    }

    [Fact]
    public void ToModelId_AppliesFairseqOffset()
    {
        Assert.Equal(XlmrPairEncoder.UnkId, XlmrPairEncoder.ToModelId(0));
        Assert.Equal(4, XlmrPairEncoder.ToModelId(3));
        Assert.Equal(new[] {35378, 8999}, XlmrPairEncoder.ToModelIds([35377, 8998]));
    }
}
