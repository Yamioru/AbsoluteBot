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

                var options = new Microsoft.ML.OnnxRuntime.SessionOptions
                {
                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                    IntraOpNumThreads = 1,
                    InterOpNumThreads = 1
                };
                _session = new InferenceSession(onnxPath, options);
                BindInputNames(_session);
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
            Log.Error(ex, "Не удалось инициализировать NLI-классификатор ачивок.");
            _ready = false;
        }
    }

    public async Task<IReadOnlyList<string>> ClassifyAsync(string text, IReadOnlyList<AchievementDefinition> catalog)
    {
        if (!_ready || _session == null || _tokenizer == null || catalog.Count == 0 || string.IsNullOrWhiteSpace(text))
            return [];

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var premise = _tokenizer.EncodeToIds(text, addBeginningOfSentence: false, addEndOfSentence: false);
            var batch = catalog.Count;
            var seq = XlmrPairEncoder.MaxLength;
            var ids = new long[batch * seq];
            var mask = new long[batch * seq];

            for (var i = 0; i < batch; i++)
            {
                var hypothesisText = "Это сообщение соответствует описанию: " + catalog[i].Criteria;
                var hypothesis = _tokenizer.EncodeToIds(hypothesisText, addBeginningOfSentence: false, addEndOfSentence: false);
                XlmrPairEncoder.EncodePair(premise, hypothesis, ids, mask, i * seq);
            }

            var idsTensor = new DenseTensor<long>(ids, [batch, seq]);
            var maskTensor = new DenseTensor<long>(mask, [batch, seq]);
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(_inputIdsName, idsTensor),
                NamedOnnxValue.CreateFromTensor(_attentionMaskName, maskTensor)
            };

            using var results = _session.Run(inputs);
            var logits = results[0].AsEnumerable<float>().ToArray();
            return NliAchievementScoring.SelectIds(
                catalog, logits, _classCount, _entailmentIndex, _contradictionIndex);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Ошибка NLI-классификации ачивки.");
            return [];
        }
        finally
        {
            _gate.Release();
        }
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
            else if (name.Contains("id", StringComparison.OrdinalIgnoreCase))
                _inputIdsName = name;
        }
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
