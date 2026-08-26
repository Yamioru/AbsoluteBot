namespace AbsoluteBot.Services.NeuralNetworkServices;

/// <summary>
///     Пара premise/hypothesis в формате XLM-RoBERTa: [CLS] A [SEP][SEP] B [SEP].
/// </summary>
public static class XlmrPairEncoder
{
    public const int BosId = 0;
    public const int PadId = 1;
    public const int EosId = 2;
    public const int UnkId = 3;
    public const int FairseqOffset = 1;
    public const int MaxLength = 128;

    /// <summary>
    ///     HuggingFace XLM-R: raw SentencePiece id 0 → unk (3), остальные +1
    ///     (fairseq зарезервировал 0..3 под &lt;s&gt;/&lt;pad&gt;/&lt;/s&gt;/&lt;unk&gt;).
    /// </summary>
    public static int ToModelId(int sentencePieceId) =>
        sentencePieceId == 0 ? UnkId : sentencePieceId + FairseqOffset;

    public static IReadOnlyList<int> ToModelIds(IReadOnlyList<int> sentencePieceIds)
    {
        var mapped = new int[sentencePieceIds.Count];
        for (var i = 0; i < sentencePieceIds.Count; i++)
            mapped[i] = ToModelId(sentencePieceIds[i]);
        return mapped;
    }

    public static void EncodePair(
        IReadOnlyList<int> premise,
        IReadOnlyList<int> hypothesis,
        long[] ids,
        long[] mask,
        int offset,
        int maxLength = MaxLength)
    {
        var special = 4;
        var budget = Math.Max(8, maxLength - special);
        var hypothesisBudget = Math.Min(hypothesis.Count, Math.Max(16, budget / 3));
        var premiseBudget = Math.Min(premise.Count, budget - hypothesisBudget);

        var index = offset;
        ids[index] = BosId;
        mask[index] = 1;
        index++;

        for (var i = 0; i < premiseBudget; i++, index++)
        {
            ids[index] = premise[i];
            mask[index] = 1;
        }

        ids[index] = EosId;
        mask[index] = 1;
        index++;
        ids[index] = EosId;
        mask[index] = 1;
        index++;

        for (var i = 0; i < hypothesisBudget; i++, index++)
        {
            ids[index] = hypothesis[i];
            mask[index] = 1;
        }

        ids[index] = EosId;
        mask[index] = 1;
        index++;

        while (index < offset + maxLength)
        {
            ids[index] = PadId;
            mask[index] = 0;
            index++;
        }
    }
}
