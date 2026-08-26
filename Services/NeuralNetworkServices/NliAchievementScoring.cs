using AbsoluteBot.Models;

namespace AbsoluteBot.Services.NeuralNetworkServices;

/// <summary>
///     Выбор ачивок по логитам NLI (entailment / neutral / contradiction).
/// </summary>
public static class NliAchievementScoring
{
    public const float DefaultMinEntailment = 0.55f;

    public static IReadOnlyList<string> SelectIds(
        IReadOnlyList<AchievementDefinition> catalog,
        float[] logits,
        int classCount,
        int entailmentIndex,
        int contradictionIndex,
        float minEntailment = DefaultMinEntailment)
    {
        if (catalog.Count == 0 || logits.Length < catalog.Count * classCount || classCount < 2)
            return Array.Empty<string>();

        var selected = new List<string>();
        for (var i = 0; i < catalog.Count; i++)
        {
            var offset = i * classCount;
            var probs = Softmax(logits, offset, classCount);
            var entailment = probs[entailmentIndex];
            var contradiction = probs[contradictionIndex];
            if (entailment >= minEntailment && entailment > contradiction)
                selected.Add(catalog[i].Id);
        }

        return selected;
    }

    internal static float[] Softmax(float[] logits, int offset, int count)
    {
        var max = logits[offset];
        for (var i = 1; i < count; i++)
            if (logits[offset + i] > max)
                max = logits[offset + i];

        var exp = new float[count];
        var sum = 0f;
        for (var i = 0; i < count; i++)
        {
            exp[i] = MathF.Exp(logits[offset + i] - max);
            sum += exp[i];
        }

        if (sum <= 0)
            return exp;
        for (var i = 0; i < count; i++)
            exp[i] /= sum;
        return exp;
    }

    internal static int FindLabelIndex(IReadOnlyDictionary<string, string> id2Label, string label, int fallback)
    {
        foreach (var pair in id2Label)
        {
            if (string.Equals(pair.Value, label, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(pair.Key, out var index))
                return index;
        }

        return fallback;
    }
}
