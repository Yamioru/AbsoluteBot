using System.Text.Json;
using AbsoluteBot.Models;
using AbsoluteBot.Services.UtilityServices;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;
using Serilog;

namespace AbsoluteBot.Services.NeuralNetworkServices;

/// <summary>
///     Локальный NLI-классификатор ачивок (multilingual MiniLMv2-L6 MNLI/XNLI ONNX).
///     Не использует Gemini/Groq/Ollama.
/// </summary>
public class OnnxNliAchievementClassifier : IAchievementClassifier, IAsyncInitializable, IDisposable
{
    public const string ModelDirectoryName = "achievement_model";
    public const string OnnxFileName = "model_quantized.onnx";
    public const string SentencePieceFileName = "sentencepiece.bpe.model";
    public const string ConfigFileName = "config.json";
    public const string HuggingFaceOnnxRepo = "onnx-community/multilingual-MiniLMv2-L6-mnli-xnli-ONNX";
    public const string HuggingFaceTokenizerRepo = "MoritzLaurer/multilingual-MiniLMv2-L6-mnli-xnli";

    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private InferenceSession? _session;
    private SentencePieceTokenizer? _tokenizer;
    private int _entailmentIndex = 0;
    private int _contradictionIndex = 2;
    private int _classCount = 3;
    private string _inputIdsName = "input_ids";
    private string _attentionMaskName = "attention_mask";
    private string? _tokenTypeName;
    private bool _ready;

    public OnnxNliAchievementClassifier(HttpClient httpClient)
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
            var directory = DataPaths.Get(ModelDirectoryName);
            Directory.CreateDirectory(directory);

            var onnxPath = Path.Combine(directory, OnnxFileName);
            var spPath = Path.Combine(directory, SentencePieceFileName);
            var configPath = Path.Combine(directory, ConfigFileName);

            await EnsureFileAsync(onnxPath, HuggingFaceOnnxRepo, "onnx/model_quantized.onnx").ConfigureAwait(false);
            await EnsureFileAsync(spPath, HuggingFaceTokenizerRepo, "sentencepiece.bpe.model").ConfigureAwait(false);
            await EnsureFileAsync(configPath, HuggingFaceOnnxRepo, "config.json").ConfigureAwait(false);

            if (!File.Exists(onnxPath) || !File.Exists(spPath))
            {
                Log.Warning("NLI-модель ачивок не загружена. Классификация отключена до появления файлов в {Dir}.", directory);
                return;
            }

            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_ready) return;

                LoadLabelMap(configPath);
                await using (var stream = File.OpenRead(spPath))
                    _tokenizer = SentencePieceTokenizer.Create(stream, addBeginningOfSentence: false, addEndOfSentence: false);

                if (!HasGlibcLoader())
                {
                    Log.Error(
                        "NLI ачивок недоступен: нет glibc (ld-linux-x86-64.so.2). Сейчас, скорее всего, Alpine. Нужен Debian-образ (Dockerfile bookworm-slim) и пересборка: !обновить. {Env}",
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
                CalibrateLabelOrder();
                _ready = true;
                Log.Information("NLI-классификатор ачивок готов ({Model}).", OnnxFileName);
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex,
                "Не удалось инициализировать NLI-классификатор ачивок. {Env}. Если в тексте ошибки ld-linux-x86-64.so.2 — контейнер Alpine, а ONNX linux-x64 нужен Debian. Пересоберите образ (!обновить).",
                DescribeNativeLoadEnvironment());
            _ready = false;
        }
    }

    public async Task<IReadOnlyList<string>> ClassifyAsync(string text, IReadOnlyList<AchievementDefinition> catalog)
    {
        var scores = await ScoreAllAsync(text, catalog).ConfigureAwait(false);
        return scores
            .Where(item => item.Entailment >= NliAchievementScoring.DefaultMinEntailment && item.Entailment > item.Contradiction)
            .Select(item => item.Id)
            .ToList();
    }

    internal async Task<IReadOnlyList<NliAchievementScore>> ScoreAllAsync(string text, IReadOnlyList<AchievementDefinition> catalog)
    {
        if (!_ready || _session == null || _tokenizer == null || catalog.Count == 0 || string.IsNullOrWhiteSpace(text))
            return Array.Empty<NliAchievementScore>();

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var premise = EncodeText(text);
            const int batchSize = 4;
            var scored = new List<NliAchievementScore>(catalog.Count);
            var seq = XlmrPairEncoder.MaxLength;

            for (var offset = 0; offset < catalog.Count; offset += batchSize)
            {
                var batch = Math.Min(batchSize, catalog.Count - offset);
                var slice = catalog.Skip(offset).Take(batch).ToList();
                var ids = new long[batch * seq];
                var mask = new long[batch * seq];
                var types = _tokenTypeName == null ? null : new long[batch * seq];

                for (var i = 0; i < batch; i++)
                {
                    var hypothesisText = ToHypothesis(slice[i]);
                    var hypothesis = EncodeText(hypothesisText);
                    XlmrPairEncoder.EncodePair(premise, hypothesis, ids, mask, i * seq);
                }

                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor(_inputIdsName, new DenseTensor<long>(ids, new[] {batch, seq})),
                    NamedOnnxValue.CreateFromTensor(_attentionMaskName, new DenseTensor<long>(mask, new[] {batch, seq}))
                };
                if (types != null && _tokenTypeName != null)
                    inputs.Add(NamedOnnxValue.CreateFromTensor(_tokenTypeName, new DenseTensor<long>(types, new[] {batch, seq})));

                using var results = _session.Run(inputs);
                var logits = results[0].AsEnumerable<float>().ToArray();
                for (var i = 0; i < batch; i++)
                {
                    var probs = NliAchievementScoring.Softmax(logits, i * _classCount, _classCount);
                    var entailment = _entailmentIndex < probs.Length ? probs[_entailmentIndex] : 0f;
                    var contradiction = _contradictionIndex < probs.Length ? probs[_contradictionIndex] : 0f;
                    var neutralIndex = 3 - _entailmentIndex - _contradictionIndex;
                    var neutral = neutralIndex >= 0 && neutralIndex < probs.Length ? probs[neutralIndex] : 0f;
                    scored.Add(new NliAchievementScore(slice[i].Id, entailment, neutral, contradiction));
                }
            }

            return scored;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Ошибка NLI-классификации ачивки.");
            return Array.Empty<NliAchievementScore>();
        }
        finally
        {
            _gate.Release();
        }
    }

    internal static string ToHypothesis(AchievementDefinition item)
    {
        if (!string.IsNullOrWhiteSpace(item.NliHypothesis))
            return item.NliHypothesis.Trim();

        var first = item.Criteria.Split('.')[0].Trim();
        if (first.EndsWith('.'))
            first = first[..^1];
        return "В этом сообщении говорится о " + first + ".";
    }

    internal IReadOnlyList<int> EncodeText(string text)
    {
        if (_tokenizer == null) return Array.Empty<int>();
        var withPrefix = text.Length > 0 && text[0] != ' ' ? " " + text : text;
        var sentencePieceIds = _tokenizer.EncodeToIds(withPrefix, addBeginningOfSentence: false, addEndOfSentence: false);
        return XlmrPairEncoder.ToModelIds(sentencePieceIds);
    }

    internal (float Entailment, float Contradiction) ScorePair(string premise, string hypothesis)
    {
        _gate.Wait();
        try
        {
            return InferPair(premise, hypothesis);
        }
        finally
        {
            _gate.Release();
        }
    }

    private void CalibrateLabelOrder()
    {
        if (_session == null || _tokenizer == null) return;

        var (entailment, contradiction) = InferPair(
            "John killed Bill.",
            "Bill is dead.");
        if (contradiction <= entailment) return;

        (_entailmentIndex, _contradictionIndex) = (_contradictionIndex, _entailmentIndex);
        Log.Information(
            "NLI: порядок меток перевёрнут после калибровки (entailment={Entailment}, contradiction={Contradiction}).",
            _entailmentIndex, _contradictionIndex);
    }

    private (float Entailment, float Contradiction) InferPair(string premiseText, string hypothesisText)
    {
        var seq = XlmrPairEncoder.MaxLength;
        var premise = EncodeText(premiseText);
        var hypothesis = EncodeText(hypothesisText);
        var ids = new long[seq];
        var mask = new long[seq];
        XlmrPairEncoder.EncodePair(premise, hypothesis, ids, mask, 0);
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(_inputIdsName, new DenseTensor<long>(ids, new[] {1, seq})),
            NamedOnnxValue.CreateFromTensor(_attentionMaskName, new DenseTensor<long>(mask, new[] {1, seq}))
        };
        if (_tokenTypeName != null)
            inputs.Add(NamedOnnxValue.CreateFromTensor(_tokenTypeName, new DenseTensor<long>(new long[seq], new[] {1, seq})));

        using var results = _session!.Run(inputs);
        var logits = results[0].AsEnumerable<float>().ToArray();
        var probs = NliAchievementScoring.Softmax(logits, 0, _classCount);
        var entailment = _entailmentIndex < probs.Length ? probs[_entailmentIndex] : 0f;
        var contradiction = _contradictionIndex < probs.Length ? probs[_contradictionIndex] : 0f;
        return (entailment, contradiction);
    }

    public void Dispose()
    {
        _session?.Dispose();
        _gate.Dispose();
        GC.SuppressFinalize(this);
    }

    internal static string ModelDirectory => DataPaths.Get(ModelDirectoryName);

    private void LoadLabelMap(string configPath)
    {
        _entailmentIndex = 0;
        _contradictionIndex = 2;
        _classCount = 3;
        if (!File.Exists(configPath)) return;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
            if (!doc.RootElement.TryGetProperty("id2label", out var map)) return;

            var id2Label = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in map.EnumerateObject())
                id2Label[property.Name] = property.Value.GetString() ?? property.Name;

            _entailmentIndex = NliAchievementScoring.FindLabelIndex(id2Label, "entailment", 0);
            _contradictionIndex = NliAchievementScoring.FindLabelIndex(id2Label, "contradiction", 2);
            _classCount = Math.Max(3, id2Label.Count);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Не удалось прочитать id2label NLI-модели, используются 0=entailment, 2=contradiction.");
        }
    }

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
            "NLI входы: ids={Ids} mask={Mask} type={Type}; labels entailment={Entailment} contradiction={Contradiction}",
            _inputIdsName, _attentionMaskName, _tokenTypeName ?? "-", _entailmentIndex, _contradictionIndex);
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
            Log.Information("Скачан файл NLI-модели {File}.", Path.GetFileName(destination));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Не удалось скачать {Url}.", url);
        }
    }
}

internal readonly record struct NliAchievementScore(string Id, float Entailment, float Neutral, float Contradiction);
