using System.Globalization;
using System.Text.RegularExpressions;
using AbsoluteBot.Models;

namespace AbsoluteBot.Services.NeuralNetworkServices;

/// <summary>
///     Выбор ачивок по косинусной близости эмбеддингов сообщения и подписи.
/// </summary>
public static partial class EmbeddingAchievementScoring
{
    public const float DefaultMinCosine = 0.82f;
    public const float DefaultSecondGap = 0.03f;
    public const float DefaultMeanGap = 0.05f;
    public const float DefaultNegativeMargin = 0.02f;
    public const float DefaultDecoyMargin = 0.02f;

    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

    public static IReadOnlyList<string> SelectIds(
        IReadOnlyList<AchievementScore> scores,
        float maxDecoy = 0f,
        float minCosine = DefaultMinCosine,
        float secondGap = DefaultSecondGap,
        float meanGap = DefaultMeanGap,
        float negativeMargin = DefaultNegativeMargin,
        float decoyMargin = DefaultDecoyMargin)
    {
        if (scores.Count == 0)
            return Array.Empty<string>();

        var mean = scores.Average(item => item.Cosine);
        var ranked = scores
            .Where(item => item.Cosine >= minCosine && item.Cosine + negativeMargin > item.NegativeCosine)
            .OrderByDescending(item => item.Cosine)
            .ToList();
        if (ranked.Count == 0)
            return Array.Empty<string>();

        var best = ranked[0];
        if (best.Cosine < mean + meanGap)
            return Array.Empty<string>();
        if (best.Cosine < maxDecoy + decoyMargin)
            return Array.Empty<string>();
        if (ranked.Count > 1 && best.Cosine - ranked[1].Cosine < secondGap)
            return Array.Empty<string>();

        return [best.Id];
    }

    public static IReadOnlyList<string> ConfirmMentions(
        string text,
        IReadOnlyList<AchievementDefinition> catalog,
        IReadOnlyList<string> ids)
    {
        if (ids.Count == 0)
            return ids;

        var byId = catalog.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        return ids
            .Where(id => byId.TryGetValue(id, out var item) && MentionsLabel(text, ToLabel(item)))
            .ToList();
    }

    public static bool MentionsLabel(string text, string label)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(label))
            return false;

        var words = SplitWords(Normalize(text));
        if (words.Count == 0)
            return false;

        foreach (var chunk in label.Split([',', ';', '/', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (var token in SplitWords(Normalize(chunk)))
            {
                if (token.Length < 3)
                    continue;
                if (MatchesToken(words, token))
                    return true;
            }
        }

        return false;
    }

    private static bool MatchesToken(IReadOnlyList<string> words, string token)
    {
        foreach (var word in words)
        {
            if (token.Length <= 3)
            {
                if (word == token)
                    return true;
                continue;
            }

            if (word == token || word.StartsWith(token, StringComparison.Ordinal) ||
                (word.Length >= 4 && token.StartsWith(word, StringComparison.Ordinal)))
                return true;
        }

        return false;
    }

    private static string Normalize(string value) =>
        value.Replace('ё', 'е').Replace('Ё', 'Е').ToLower(Ru);

    private static List<string> SplitWords(string value)
    {
        var words = new List<string>();
        foreach (Match match in WordRegex().Matches(value))
            words.Add(match.Value);
        return words;
    }

    [GeneratedRegex(@"[а-яa-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex WordRegex();

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

public readonly record struct AchievementScoreBatch(IReadOnlyList<AchievementScore> Scores, float MaxDecoy);
