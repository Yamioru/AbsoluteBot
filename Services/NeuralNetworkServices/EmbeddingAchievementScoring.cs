using AbsoluteBot.Models;

namespace AbsoluteBot.Services.NeuralNetworkServices;

/// <summary>
///     Выбор ачивок по косинусной близости эмбеддингов сообщения и подписи.
/// </summary>
public static class EmbeddingAchievementScoring
{
    public const float DefaultMinCosine = 0.80f;
    public const float DefaultPeakMargin = 0.02f;
    public const float DefaultMeanGap = 0.03f;
    public const float DefaultNegativeMargin = 0.02f;

    public static IReadOnlyList<string> SelectIds(
        IReadOnlyList<AchievementScore> scores,
        float minCosine = DefaultMinCosine,
        float peakMargin = DefaultPeakMargin,
        float meanGap = DefaultMeanGap,
        float negativeMargin = DefaultNegativeMargin)
    {
        if (scores.Count == 0)
            return Array.Empty<string>();

        var mean = scores.Average(item => item.Cosine);
        var eligible = scores
            .Where(item => item.Cosine >= minCosine && item.Cosine + negativeMargin > item.NegativeCosine)
            .ToList();
        if (eligible.Count == 0)
            return Array.Empty<string>();

        var max = eligible.Max(item => item.Cosine);
        if (max < mean + meanGap)
            return Array.Empty<string>();

        return eligible
            .Where(item => item.Cosine >= max - peakMargin)
            .Select(item => item.Id)
            .ToList();
    }

    public static float Cosine(ReadOnlySpan<float> left, ReadOnlySpan<float> right)
    {
        if (left.Length == 0 || left.Length != right.Length)
            return 0f;

        var sum = 0f;
        for (var i = 0; i < left.Length; i++)
            sum += left[i] * right[i];
        return sum;
    }

    public static float[] MeanPool(float[] hidden, long[] mask, int batchIndex, int sequenceLength, int hiddenSize)
    {
        var pooled = new float[hiddenSize];
        var count = 0f;
        var row = batchIndex * sequenceLength;
        for (var token = 0; token < sequenceLength; token++)
        {
            if (mask[row + token] == 0)
                continue;
            count++;
            var offset = ((row + token) * hiddenSize);
            for (var i = 0; i < hiddenSize; i++)
                pooled[i] += hidden[offset + i];
        }

        if (count <= 0)
            return pooled;
        for (var i = 0; i < hiddenSize; i++)
            pooled[i] /= count;
        return L2Normalize(pooled);
    }

    public static float[] L2Normalize(float[] vector)
    {
        var sum = 0f;
        for (var i = 0; i < vector.Length; i++)
            sum += vector[i] * vector[i];
        var norm = MathF.Sqrt(sum);
        if (norm < 1e-8f)
            return vector;
        for (var i = 0; i < vector.Length; i++)
            vector[i] /= norm;
        return vector;
    }

    public static string ToLabel(AchievementDefinition item)
    {
        if (!string.IsNullOrWhiteSpace(item.NliHypothesis))
            return item.NliHypothesis.Trim();

        var first = item.Criteria.Split('.')[0].Trim();
        if (first.EndsWith('.'))
            first = first[..^1];
        return first;
    }

    public static string ToQuery(string text) => "query: " + text.Trim();

    public static string ToPassage(string text) => "passage: " + text.Trim();
}

public readonly record struct AchievementScore(string Id, float Cosine, float NegativeCosine);
