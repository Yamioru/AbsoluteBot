using AbsoluteBot.Models;
using AbsoluteBot.Services.UtilityServices;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;
using Serilog;

namespace AbsoluteBot.Services.NeuralNetworkServices;

/// <summary>
///     Локальный классификатор ачивок: multilingual-e5-small (ONNX), косинус к подписям каталога.
///     Не использует Gemini/Groq/Ollama и не использует NLI MiniLM.
/// </summary>
public class OnnxEmbeddingAchievementClassifier : IAchievementClassifier, IAsyncInitializable, IDisposable
{
    public const string ModelDirectoryName = "achievement_model";
    public const string ModelSubdirectory = "e5-small";
    public const string OnnxFileName = "model_quantized.onnx";
    public const string SentencePieceFileName = "sentencepiece.bpe.model";
    public const string HuggingFaceOnnxRepo = "Xenova/multilingual-e5-small";
    public const string HuggingFaceTokenizerRepo = "intfloat/multilingual-e5-small";

    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private static readonly string[] DecoyPassages =
    [
        "обычное короткое сообщение в чате: привет, ок, лол, ага",
        "оскорбление человека по имени: ты чёрт, дурак, идиот, паша ты чёрт",
        "обращение к человеку без темы разговора"
    ];
    private readonly Dictionary<string, float[]> _labelCache = new(StringComparer.Ordinal);
    private InferenceSession? _session;
    private SentencePieceTokenizer? _tokenizer;
    private string _inputIdsName = "input_ids";
    private string _attentionMaskName = "attention_mask";
    private string? _tokenTypeName;
    private int _hiddenSize = 384;
    private bool _outputIsPooled;
    private bool _ready;

    public OnnxEmbeddingAchievementClassifier(HttpClient httpClient)
    {
        _httpClient = httpClient;
        if (_httpClient.Timeout < TimeSpan.FromMinutes(2))
            _httpClient.Timeout = TimeSpan.FromMinutes(8);
    }

    public Task InitializeAsync()
    {
        _ = Task.Run(LoadOrDownloadAsync);
        return Task.CompletedTask;
    }

    internal bool IsReady => _ready;

    internal Task LoadModelAsync() => LoadOrDownloadAsync();

    private async Task LoadOrDownloadAsync()
    {
        try
        {
            var directory = Path.Combine(DataPaths.Get(ModelDirectoryName), ModelSubdirectory);
            Directory.CreateDirectory(directory);

            var onnxPath = Path.Combine(directory, OnnxFileName);
            var spPath = Path.Combine(directory, SentencePieceFileName);

            await EnsureFileAsync(onnxPath, HuggingFaceOnnxRepo, "onnx/model_quantized.onnx").ConfigureAwait(false);
            await EnsureFileAsync(spPath, HuggingFaceTokenizerRepo, SentencePieceFileName).ConfigureAwait(false);

            if (!File.Exists(onnxPath) || !File.Exists(spPath))
            {
                Log.Warning("e5-модель ачивок не загружена. Классификация отключена до появления файлов в {Dir}.", directory);
                return;
            }

            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_ready) return;

                await using (var stream = File.OpenRead(spPath))
                    _tokenizer = SentencePieceTokenizer.Create(stream, addBeginningOfSentence: false, addEndOfSentence: false);

                if (!HasGlibcLoader())
                {
                    Log.Error(
                        "Эмбеддинги ачивок недоступны: нет glibc (ld-linux-x86-64.so.2). Сейчас, скорее всего, Alpine. Нужен Debian-образ (Dockerfile bookworm-slim) и пересборка: !обновить. {Env}",
                        DescribeNativeLoadEnvironment());
                    return;
                }

                var options = new Microsoft.ML.OnnxRuntime.SessionOptions
                {
                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_BASIC,
                    IntraOpNumThreads = 1,
                    InterOpNumThreads = 1,
                    EnableMemoryPattern = false,
                    EnableCpuMemArena = false
                };
                _session = new InferenceSession(onnxPath, options);
                BindInputNames(_session);
                BindOutputShape(_session);
                _ready = true;
                Log.Information("Эмбеддинг-классификатор ачивок готов (e5-small, hidden={Hidden}).", _hiddenSize);
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex,
                "Не удалось инициализировать эмбеддинг-классификатор ачивок. {Env}. Если в тексте ошибки ld-linux-x86-64.so.2 — контейнер Alpine, а ONNX linux-x64 нужен Debian. Пересоберите образ (!обновить).",
                DescribeNativeLoadEnvironment());
            _ready = false;
        }
    }

    public async Task<IReadOnlyList<string>> ClassifyAsync(string text, IReadOnlyList<AchievementDefinition> catalog)
    {
        var scores = await ScoreAllAsync(text, catalog).ConfigureAwait(false);
        var mentioned = scores.Scores
            .Where(item => catalog.Any(definition =>
                string.Equals(definition.Id, item.Id, StringComparison.OrdinalIgnoreCase)
                && EmbeddingAchievementScoring.MentionsLabel(text, EmbeddingAchievementScoring.ToLabel(definition))))
            .ToList();
        return EmbeddingAchievementScoring.SelectIds(mentioned, scores.MaxDecoy, meanGap: 0f, secondGap: 0.02f);
    }

    internal async Task<AchievementScoreBatch> ScoreAllAsync(string text, IReadOnlyList<AchievementDefinition> catalog)
    {
        if (!_ready || _session == null || _tokenizer == null || catalog.Count == 0 || string.IsNullOrWhiteSpace(text))
            return new AchievementScoreBatch([], 0f);

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var query = Embed(EmbeddingAchievementScoring.ToQuery(text));
            var scored = new List<AchievementScore>(catalog.Count);
            foreach (var item in catalog)
            {
                var label = EmbeddingAchievementScoring.ToLabel(item);
                var positive = EmbedCached(EmbeddingAchievementScoring.ToPassage(label));
                var cosine = EmbeddingAchievementScoring.Cosine(query, positive);
                var negative = 0f;
                if (!string.IsNullOrWhiteSpace(item.NegativeHypothesis))
                {
                    var negativeVector = EmbedCached(EmbeddingAchievementScoring.ToPassage(item.NegativeHypothesis.Trim()));
                    negative = EmbeddingAchievementScoring.Cosine(query, negativeVector);
                }

                scored.Add(new AchievementScore(item.Id, cosine, negative));
            }

            var maxDecoy = 0f;
            foreach (var decoy in DecoyPassages)
            {
                var vector = EmbedCached(EmbeddingAchievementScoring.ToPassage(decoy));
                maxDecoy = Math.Max(maxDecoy, EmbeddingAchievementScoring.Cosine(query, vector));
            }

            return new AchievementScoreBatch(scored, maxDecoy);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Ошибка эмбеддинг-классификации ачивки.");
            return new AchievementScoreBatch([], 0f);
        }
        finally
        {
            _gate.Release();
        }
    }

    internal IReadOnlyList<int> EncodeText(string text)
    {
        if (_tokenizer == null) return Array.Empty<int>();
        var withPrefix = text.Length > 0 && text[0] != ' ' ? " " + text : text;
        var sentencePieceIds = _tokenizer.EncodeToIds(withPrefix, addBeginningOfSentence: false, addEndOfSentence: false);
        return XlmrPairEncoder.ToModelIds(sentencePieceIds);
    }

    internal float Cosine(string left, string right)
    {
        _gate.Wait();
        try
        {
            var a = Embed(EmbeddingAchievementScoring.ToQuery(left));
            var b = Embed(EmbeddingAchievementScoring.ToPassage(right));
            return EmbeddingAchievementScoring.Cosine(a, b);
        }
        finally
        {
            _gate.Release();
        }
    }

    private float[] EmbedCached(string passage)
    {
        if (_labelCache.TryGetValue(passage, out var cached))
            return cached;
        var vector = Embed(passage);
        _labelCache[passage] = vector;
        return vector;
    }

    private float[] Embed(string text)
    {
        var seq = XlmrPairEncoder.MaxLength;
        var tokens = EncodeText(text);
        var ids = new long[seq];
        var mask = new long[seq];
        XlmrPairEncoder.EncodeSingle(tokens, ids, mask, 0);
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(_inputIdsName, new DenseTensor<long>(ids, new[] {1, seq})),
            NamedOnnxValue.CreateFromTensor(_attentionMaskName, new DenseTensor<long>(mask, new[] {1, seq}))
        };
        if (_tokenTypeName != null)
            inputs.Add(NamedOnnxValue.CreateFromTensor(_tokenTypeName, new DenseTensor<long>(new long[seq], new[] {1, seq})));

        using var results = _session!.Run(inputs);
        var hidden = results[0].AsEnumerable<float>().ToArray();
        if (_outputIsPooled)
        {
            var vector = hidden.Length > _hiddenSize ? hidden[.._hiddenSize] : hidden;
            return EmbeddingAchievementScoring.L2Normalize((float[])vector.Clone());
        }

        if (seq > 0 && hidden.Length % seq == 0)
            _hiddenSize = hidden.Length / seq;
        return EmbeddingAchievementScoring.MeanPool(hidden, mask, 0, seq, _hiddenSize);
    }

    public void Dispose()
    {
        _session?.Dispose();
        _gate.Dispose();
        GC.SuppressFinalize(this);
    }

    internal static string ModelDirectory => Path.Combine(DataPaths.Get(ModelDirectoryName), ModelSubdirectory);

    private void BindInputNames(InferenceSession session)
    {
        foreach (var name in session.InputMetadata.Keys)
        {
            if (name.Contains("mask", StringComparison.OrdinalIgnoreCase))
                _attentionMaskName = name;
            else if (name.Contains("type", StringComparison.OrdinalIgnoreCase))
                _tokenTypeName = name;
            else if (name.Contains("id", StringComparison.OrdinalIgnoreCase))
                _inputIdsName = name;
        }

        Log.Information(
            "e5 входы: ids={Ids} mask={Mask} type={Type}",
            _inputIdsName, _attentionMaskName, _tokenTypeName ?? "-");
    }

    private void BindOutputShape(InferenceSession session)
    {
        var output = session.OutputMetadata.Values.First();
        var dims = output.Dimensions;
        if (dims.Length == 2)
        {
            _outputIsPooled = true;
            _hiddenSize = dims[1] > 0 ? dims[1] : 384;
        }
        else if (dims.Length >= 3)
        {
            _outputIsPooled = false;
            _hiddenSize = dims[2] > 0 ? dims[2] : 384;
        }
    }

    internal static string DescribeNativeLoadEnvironment()
    {
        var os = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
        var rid = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier;
        return $"OS={os}; RID={rid}; glibcLoader={HasGlibcLoader()}";
    }

    internal static bool HasGlibcLoader()
    {
        return File.Exists("/lib/x86_64-linux-gnu/ld-linux-x86-64.so.2")
               || File.Exists("/lib64/ld-linux-x86-64.so.2")
               || OperatingSystem.IsWindows();
    }

    private async Task EnsureFileAsync(string destination, string repo, string relativePath)
    {
        if (File.Exists(destination) && new FileInfo(destination).Length > 0)
            return;

        var url = $"https://huggingface.co/{repo}/resolve/main/{relativePath}";
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("User-Agent", "AbsoluteBot/1.0");
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var temp = destination + ".tmp";
            await using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
            await using (var output = File.Create(temp))
                await input.CopyToAsync(output).ConfigureAwait(false);
            File.Move(temp, destination, true);
            Log.Information("Скачан файл e5-модели {File}.", Path.GetFileName(destination));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Не удалось скачать {Url}.", url);
        }
    }
}
