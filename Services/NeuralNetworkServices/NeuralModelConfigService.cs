using AbsoluteBot.Services.UtilityServices;
using Newtonsoft.Json;
using Serilog;

namespace AbsoluteBot.Services.NeuralNetworkServices;

/// <summary>
///     Хранит текущего провайдера (Gemini/Groq) и модели по сущностям в <c>neural_models.json</c>.
///     Изменения применяются сразу, без перезапуска.
/// </summary>
public class NeuralModelConfigService : IAsyncInitializable
{
    private static string FilePath => DataPaths.Get("neural_models.json");
    private static readonly SemaphoreSlim Semaphore = new(1, 1);
    private NeuralModelsDocument _document = NeuralModelsDocument.CreateDefault();
    private bool _loaded;

    public async Task InitializeAsync()
    {
        await EnsureLoadedAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     Возвращает текущего провайдера (<c>gemini</c> или <c>groq</c>).
    /// </summary>
    public string GetProvider()
    {
        EnsureLoaded();
        return _document.Provider;
    }

    /// <summary>
    ///     Возвращает модель для сущности с учётом текущего провайдера.
    ///     Сущность <see cref="NeuralEntities.Image" /> всегда берётся из Gemini.
    /// </summary>
    public string GetModel(string entity)
    {
        EnsureLoaded();
        var normalized = NormalizeEntity(entity);
        if (normalized == null)
            throw new ArgumentException($"Неизвестная сущность нейросети: {entity}", nameof(entity));

        if (string.Equals(normalized, NeuralEntities.Image, StringComparison.OrdinalIgnoreCase))
            return GetModelFromMap(_document.Gemini, NeuralEntities.Image, NeuralModelsDocument.DefaultGeminiImage);

        var map = IsGroq() ? _document.Groq : _document.Gemini;
        var fallback = NeuralModelsDocument.GetDefaultModel(normalized, IsGroq() ? NeuralProviders.Groq : NeuralProviders.Gemini);
        return GetModelFromMap(map, normalized, fallback);
    }

    /// <summary>
    ///     Переключает провайдера и сразу сохраняет файл.
    /// </summary>
    public async Task<bool> SetProviderAsync(string provider)
    {
        var normalized = NormalizeProvider(provider);
        if (normalized == null)
            return false;

        await Semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            await LoadIfNeededUnsafeAsync().ConfigureAwait(false);
            _document.Provider = normalized;
            await SaveUnsafeAsync().ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Не удалось сохранить провайдера нейросети.");
            return false;
        }
        finally
        {
            Semaphore.Release();
        }
    }

    /// <summary>
    ///     Задаёт модель для сущности. Если провайдер не указан — используется текущий.
    ///     Image всегда пишется в секцию Gemini.
    /// </summary>
    public async Task<bool> SetModelAsync(string entity, string modelId, string? provider = null)
    {
        var normalizedEntity = NormalizeEntity(entity);
        if (normalizedEntity == null || string.IsNullOrWhiteSpace(modelId))
            return false;

        string? normalizedProvider;
        if (string.Equals(normalizedEntity, NeuralEntities.Image, StringComparison.OrdinalIgnoreCase))
        {
            normalizedProvider = NeuralProviders.Gemini;
        }
        else
        {
            normalizedProvider = provider == null ? null : NormalizeProvider(provider);
            if (provider != null && normalizedProvider == null)
                return false;
        }

        await Semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            await LoadIfNeededUnsafeAsync().ConfigureAwait(false);
            var targetProvider = normalizedProvider ?? _document.Provider;
            var map = string.Equals(targetProvider, NeuralProviders.Groq, StringComparison.OrdinalIgnoreCase)
                ? _document.Groq
                : _document.Gemini;
            map[normalizedEntity] = modelId.Trim();
            await SaveUnsafeAsync().ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Не удалось сохранить модель нейросети.");
            return false;
        }
        finally
        {
            Semaphore.Release();
        }
    }

    /// <summary>
    ///     Текстовое представление текущего провайдера и всех моделей (для админ-команды).
    /// </summary>
    public string FormatForDisplay()
    {
        EnsureLoaded();
        var lines = new List<string>
        {
            $"Провайдер: {_document.Provider}"
        };

        lines.Add("Gemini:");
        foreach (var entity in NeuralEntities.All)
            lines.Add($"  {entity}: {GetModelFromMap(_document.Gemini, entity, NeuralModelsDocument.GetDefaultModel(entity, NeuralProviders.Gemini))}");

        lines.Add("Groq:");
        foreach (var entity in NeuralEntities.All)
        {
            if (entity == NeuralEntities.Image)
            {
                lines.Add("  Image: (всегда Gemini)");
                continue;
            }

            lines.Add($"  {entity}: {GetModelFromMap(_document.Groq, entity, NeuralModelsDocument.GetDefaultModel(entity, NeuralProviders.Groq))}");
        }

        return string.Join("\n", lines);
    }

    public static string? NormalizeProvider(string? provider)
    {
        if (string.Equals(provider, NeuralProviders.Gemini, StringComparison.OrdinalIgnoreCase))
            return NeuralProviders.Gemini;
        if (string.Equals(provider, NeuralProviders.Groq, StringComparison.OrdinalIgnoreCase))
            return NeuralProviders.Groq;
        return null;
    }

    public static string? NormalizeEntity(string? entity)
    {
        if (string.IsNullOrWhiteSpace(entity))
            return null;
        foreach (var known in NeuralEntities.All)
            if (string.Equals(known, entity, StringComparison.OrdinalIgnoreCase))
                return known;
        return null;
    }

    private bool IsGroq() =>
        string.Equals(_document.Provider, NeuralProviders.Groq, StringComparison.OrdinalIgnoreCase);

    private static string GetModelFromMap(Dictionary<string, string> map, string entity, string fallback)
    {
        if (map.TryGetValue(entity, out var model) && !string.IsNullOrWhiteSpace(model))
            return model;
        return fallback;
    }

    private void EnsureLoaded()
    {
        if (_loaded) return;
        EnsureLoadedAsync().GetAwaiter().GetResult();
    }

    private async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        await Semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            await LoadIfNeededUnsafeAsync().ConfigureAwait(false);
        }
        finally
        {
            Semaphore.Release();
        }
    }

    private async Task LoadIfNeededUnsafeAsync()
    {
        if (_loaded) return;
        _document = await LoadFromFileAsync().ConfigureAwait(false);
        _loaded = true;
    }

    private static async Task<NeuralModelsDocument> LoadFromFileAsync()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                var created = NeuralModelsDocument.CreateDefault();
                await WriteFileAsync(created).ConfigureAwait(false);
                return created;
            }

            var json = await File.ReadAllTextAsync(FilePath).ConfigureAwait(false);
            var document = JsonConvert.DeserializeObject<NeuralModelsDocument>(json) ?? NeuralModelsDocument.CreateDefault();
            document.EnsureDefaults();
            return document;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Не удалось загрузить neural_models.json, используются значения по умолчанию.");
            return NeuralModelsDocument.CreateDefault();
        }
    }

    private async Task SaveUnsafeAsync()
    {
        _document.EnsureDefaults();
        await WriteFileAsync(_document).ConfigureAwait(false);
    }

    private static async Task WriteFileAsync(NeuralModelsDocument document)
    {
        var json = JsonConvert.SerializeObject(document, Formatting.Indented);
        var tempFilePath = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFilePath, json).ConfigureAwait(false);
        await Task.Run(() => File.Move(tempFilePath, FilePath, true)).ConfigureAwait(false);
    }
}

/// <summary>
///     Документ <c>neural_models.json</c>.
/// </summary>
public class NeuralModelsDocument
{
    public const string DefaultGeminiChat = "gemini-3.5-flash";
    public const string DefaultGeminiAsk = "gemini-3.1-flash-lite";
    public const string DefaultGeminiGoogleSearch = "gemini-3.5-flash";
    public const string DefaultGeminiImage = "gemini-3-pro-image-preview";
    public const string DefaultGroqChat = "openai/gpt-oss-20b";
    public const string DefaultGroqAsk = "openai/gpt-oss-20b";
    public const string DefaultGroqGoogleSearch = "groq/compound";

    [JsonProperty("provider")]
    public string Provider { get; set; } = NeuralProviders.Gemini;

    [JsonProperty("gemini")]
    public Dictionary<string, string> Gemini { get; set; } = new();

    [JsonProperty("groq")]
    public Dictionary<string, string> Groq { get; set; } = new();

    public static NeuralModelsDocument CreateDefault()
    {
        var document = new NeuralModelsDocument();
        document.EnsureDefaults();
        return document;
    }

    public void EnsureDefaults()
    {
        if (NeuralModelConfigService.NormalizeProvider(Provider) == null)
            Provider = NeuralProviders.Gemini;

        Gemini ??= new Dictionary<string, string>();
        Groq ??= new Dictionary<string, string>();

        SetIfMissing(Gemini, NeuralEntities.Chat, DefaultGeminiChat);
        SetIfMissing(Gemini, NeuralEntities.Ask, DefaultGeminiAsk);
        SetIfMissing(Gemini, NeuralEntities.GoogleSearch, DefaultGeminiGoogleSearch);
        SetIfMissing(Gemini, NeuralEntities.Image, DefaultGeminiImage);

        SetIfMissing(Groq, NeuralEntities.Chat, DefaultGroqChat);
        SetIfMissing(Groq, NeuralEntities.Ask, DefaultGroqAsk);
        SetIfMissing(Groq, NeuralEntities.GoogleSearch, DefaultGroqGoogleSearch);
    }

    public static string GetDefaultModel(string entity, string provider)
    {
        if (string.Equals(entity, NeuralEntities.Image, StringComparison.OrdinalIgnoreCase))
            return DefaultGeminiImage;
        if (string.Equals(provider, NeuralProviders.Groq, StringComparison.OrdinalIgnoreCase))
            return entity switch
            {
                NeuralEntities.GoogleSearch => DefaultGroqGoogleSearch,
                _ => DefaultGroqChat
            };
        return entity switch
        {
            NeuralEntities.Ask => DefaultGeminiAsk,
            NeuralEntities.GoogleSearch => DefaultGeminiGoogleSearch,
            NeuralEntities.Image => DefaultGeminiImage,
            _ => DefaultGeminiChat
        };
    }

    private static void SetIfMissing(Dictionary<string, string> map, string key, string value)
    {
        if (!map.TryGetValue(key, out var existing) || string.IsNullOrWhiteSpace(existing))
            map[key] = value;
    }
}
