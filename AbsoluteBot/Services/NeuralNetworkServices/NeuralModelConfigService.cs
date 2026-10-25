using AbsoluteBot.Services.UtilityServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace AbsoluteBot.Services.NeuralNetworkServices;

/// <summary>
/// Хранит текущего провайдера (Gemini/Groq) и модели по сущностям в <c>neural_models.json</c>.
/// Изменения применяются сразу, без перезапуска.
/// </summary>
public class NeuralModelConfigService : IAsyncInitializable
{
    private static readonly SemaphoreSlim Semaphore = new(1, 1);
    private bool _loaded;
    private NeuralModelsDocument _document = NeuralModelsDocument.CreateDefault();
    private static string FilePath => DataPaths.Get("neural_models.json");

    public async Task InitializeAsync()
    {
        await EnsureLoadedAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Текстовое представление текущего провайдера и всех моделей (для админ-команды).
    /// </summary>
    public string FormatForDisplay()
    {
        EnsureLoaded();
        var lines = new List<string>
        {
            $"Провайдер: {_document.Provider}",
            $"Режим API: {FormatTier(GetTier())}"
        };

        lines.Add($"!нарисуй: Cloudflare {CloudflareFluxImageService.ModelId}");
        lines.Add("Gemini:");
        foreach (var entity in NeuralEntities.All)
        {
            var choice = GetChoice(_document.Gemini, entity, NeuralProviders.Gemini);
            var suffix = entity == NeuralEntities.Image ? " (запасной, не используется !нарисуй)" : "";
            lines.Add($"  {entity}: {FormatChoice(choice)}{suffix}");
        }

        lines.Add("Groq:");
        foreach (var entity in NeuralEntities.All)
        {
            if (entity == NeuralEntities.Image)
            {
                lines.Add("  Image: (запасной Gemini, не используется !нарисуй)");
                continue;
            }

            lines.Add($"  {entity}: {FormatChoice(GetChoice(_document.Groq, entity, NeuralProviders.Groq))}");
        }

        return string.Join("\n", lines);
    }

    public static string FormatTier(string tier)
    {
        return string.Equals(NormalizeTier(tier), NeuralTiers.Paid, StringComparison.Ordinal) ? "платный" : "бесплатный";
    }

    /// <summary>
    /// Основная модель и замена. Замена пропускается, если она пустая или совпадает с основной.
    /// </summary>
    public IReadOnlyList<string> GetAttemptModels(string entity, string? provider = null)
    {
        EnsureLoaded();
        var normalized = NormalizeEntity(entity);
        if (normalized == null)
            throw new ArgumentException($"Неизвестная сущность нейросети: {entity}", nameof(entity));

        if (string.Equals(normalized, NeuralEntities.Image, StringComparison.OrdinalIgnoreCase))
            return AttemptList(GetChoice(_document.Gemini, NeuralEntities.Image, NeuralProviders.Gemini));

        var target = provider == null ? _document.Provider : NormalizeProvider(provider) ?? _document.Provider;
        var map = string.Equals(target, NeuralProviders.Groq, StringComparison.OrdinalIgnoreCase)
            ? _document.Groq
            : _document.Gemini;
        return AttemptList(GetChoice(map, normalized, target));
    }

    /// <summary>
    /// Модели Gemini для сущности, даже если чат сейчас смотрит в Groq.
    /// </summary>
    public IReadOnlyList<string> GetGeminiAttemptModels(string entity)
    {
        return GetAttemptModels(entity, NeuralProviders.Gemini);
    }

    /// <summary>
    /// Модель Gemini для сущности, даже если чат сейчас смотрит в Groq.
    /// </summary>
    public string GetGeminiModel(string entity)
    {
        EnsureLoaded();
        var normalized = NormalizeEntity(entity);
        if (normalized == null)
            throw new ArgumentException($"Неизвестная сущность нейросети: {entity}", nameof(entity));

        return GetChoice(_document.Gemini, normalized, NeuralProviders.Gemini).Model;
    }

    /// <summary>
    /// Возвращает модель для сущности с учётом текущего провайдера.
    /// Сущность <see cref="NeuralEntities.Image" /> всегда берётся из Gemini
    /// (запасной конфиг для устаревшего Gemini image-клиента; живой <c>!нарисуй</c> идёт через Cloudflare).
    /// </summary>
    public string GetModel(string entity)
    {
        EnsureLoaded();
        var normalized = NormalizeEntity(entity);
        if (normalized == null)
            throw new ArgumentException($"Неизвестная сущность нейросети: {entity}", nameof(entity));

        return GetAttemptModels(normalized)[0];
    }

    /// <summary>
    /// Возвращает текущего провайдера (<c>gemini</c> или <c>groq</c>).
    /// </summary>
    public string GetProvider()
    {
        EnsureLoaded();
        return _document.Provider;
    }

    /// <summary>
    /// Текущий режим: <c>free</c> или <c>paid</c>. Пустое и неизвестное значение — бесплатный.
    /// </summary>
    public string GetTier()
    {
        EnsureLoaded();
        return NormalizeTier(_document.Tier) ?? NeuralTiers.Free;
    }

    /// <summary>
    /// Бесплатный режим не вызывает инструмент Google Search.
    /// </summary>
    public bool IsFreeTier()
    {
        return !string.Equals(GetTier(), NeuralTiers.Paid, StringComparison.OrdinalIgnoreCase);
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

    public static string? NormalizeProvider(string? provider)
    {
        if (string.Equals(provider, NeuralProviders.Gemini, StringComparison.OrdinalIgnoreCase))
            return NeuralProviders.Gemini;
        if (string.Equals(provider, NeuralProviders.Groq, StringComparison.OrdinalIgnoreCase))
            return NeuralProviders.Groq;
        return null;
    }

    public static string? NormalizeTier(string? tier)
    {
        if (string.Equals(tier, NeuralTiers.Free, StringComparison.OrdinalIgnoreCase)
            || string.Equals(tier, "бесплатный", StringComparison.OrdinalIgnoreCase))
            return NeuralTiers.Free;
        if (string.Equals(tier, NeuralTiers.Paid, StringComparison.OrdinalIgnoreCase)
            || string.Equals(tier, "платный", StringComparison.OrdinalIgnoreCase))
            return NeuralTiers.Paid;
        return null;
    }

    /// <summary>
    /// Задаёт модель для сущности. Если провайдер не указан — используется текущий.
    /// Image всегда пишется в секцию Gemini (запасной конфиг, не влияет на живой <c>!нарисуй</c>).
    /// </summary>
    public async Task<bool> SetModelAsync(string entity, string modelId, string? provider = null,
        string? fallback = null, bool setFallback = false)
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
            if (!map.TryGetValue(normalizedEntity, out var choice) || choice == null)
                choice = new NeuralModelChoice();
            choice.Model = modelId.Trim();
            if (setFallback)
            {
                choice.Fallback = NormalizeFallback(fallback);
                choice.FallbackSpecified = true;
            }

            map[normalizedEntity] = choice;
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
    /// Переключает провайдера и сразу сохраняет файл.
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
    /// Переключает бесплатный и платный режим API и сразу сохраняет файл.
    /// </summary>
    public async Task<bool> SetTierAsync(string tier)
    {
        var normalized = NormalizeTier(tier);
        if (normalized == null)
            return false;

        await Semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            await LoadIfNeededUnsafeAsync().ConfigureAwait(false);
            _document.Tier = normalized;
            await SaveUnsafeAsync().ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Не удалось сохранить режим API.");
            return false;
        }
        finally
        {
            Semaphore.Release();
        }
    }

    private static IReadOnlyList<string> AttemptList(NeuralModelChoice choice)
    {
        if (!choice.HasDistinctFallback)
            return new[] {choice.Model};
        return new[] {choice.Model, choice.Fallback!.Trim()};
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

    private static string FormatChoice(NeuralModelChoice choice)
    {
        return choice.HasDistinctFallback
            ? $"{choice.Model}, замена {choice.Fallback!.Trim()}"
            : $"{choice.Model}, замена не задана";
    }

    private NeuralModelChoice GetChoice(Dictionary<string, NeuralModelChoice> map, string entity, string provider)
    {
        if (map.TryGetValue(entity, out var choice) && choice != null && !string.IsNullOrWhiteSpace(choice.Model))
            return choice;
        return new NeuralModelChoice
        {
            Model = NeuralModelsDocument.GetDefaultModel(entity, provider)
        };
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
            var rawTier = document.Tier;
            document.EnsureDefaults();
            if (!string.Equals(rawTier, document.Tier, StringComparison.Ordinal) || document.ApplyMissingFallbacks())
                await WriteFileAsync(document).ConfigureAwait(false);
            return document;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Не удалось загрузить neural_models.json, используются значения по умолчанию.");
            return NeuralModelsDocument.CreateDefault();
        }
    }

    private async Task LoadIfNeededUnsafeAsync()
    {
        if (_loaded) return;
        _document = await LoadFromFileAsync().ConfigureAwait(false);
        _loaded = true;
    }

    private static string NormalizeFallback(string? fallback)
    {
        if (string.IsNullOrWhiteSpace(fallback))
            return "";
        var trimmed = fallback.Trim();
        if (trimmed is "-" or "нет" or "none")
            return "";
        return trimmed;
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
/// Документ <c>neural_models.json</c>.
/// </summary>
public class NeuralModelsDocument
{
    public const string DefaultGeminiChat = "gemini-3.8-flash";
    public const string DefaultGeminiAsk = "gemini-3.8-flash";
    public const string DefaultGeminiGoogleSearch = "gemini-3.8-flash";
    public const string DefaultGeminiImage = "gemini-3-pro-image-preview";
    public const string DefaultGeminiChatFallback = "gemini-3.5-flash-lite";
    public const string DefaultGeminiAskFallback = "gemini-3.5-flash-lite";
    public const string DefaultGeminiGoogleSearchFallback = "gemini-3.5-flash-lite";
    public const string DefaultGroqChat = "openai/gpt-oss-20b";
    public const string DefaultGroqAsk = "openai/gpt-oss-20b";
    public const string DefaultGroqGoogleSearch = "groq/compound";
    [JsonProperty("gemini")] public Dictionary<string, NeuralModelChoice> Gemini { get; set; } = new();
    [JsonProperty("groq")] public Dictionary<string, NeuralModelChoice> Groq { get; set; } = new();
    [JsonProperty("provider")] public string Provider { get; set; } = NeuralProviders.Gemini;
    [JsonProperty("tier")] public string? Tier { get; set; }

    /// <summary>
    /// Старый файл хранил одну строку на сущность. Для таких записей подставляется замена Gemini,
    /// если она ещё не была задана явно.
    /// </summary>
    public bool ApplyMissingFallbacks()
    {
        var changed = false;
        changed |= ApplyGeminiFallback(NeuralEntities.Chat, DefaultGeminiChatFallback, DefaultGeminiAskFallback);
        changed |= ApplyGeminiFallback(NeuralEntities.Ask, DefaultGeminiAskFallback, DefaultGeminiChatFallback);
        changed |= ApplyGeminiFallback(NeuralEntities.GoogleSearch, DefaultGeminiGoogleSearchFallback, DefaultGeminiAskFallback);
        return changed;
    }

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
        Tier = NeuralModelConfigService.NormalizeTier(Tier) ?? NeuralTiers.Free;

        Gemini ??= new Dictionary<string, NeuralModelChoice>();
        Groq ??= new Dictionary<string, NeuralModelChoice>();

        SetIfMissing(Gemini, NeuralEntities.Chat, DefaultGeminiChat, DefaultGeminiChatFallback);
        SetIfMissing(Gemini, NeuralEntities.Ask, DefaultGeminiAsk, DefaultGeminiAskFallback);
        SetIfMissing(Gemini, NeuralEntities.GoogleSearch, DefaultGeminiGoogleSearch, DefaultGeminiGoogleSearchFallback);
        SetIfMissing(Gemini, NeuralEntities.Image, DefaultGeminiImage, null);

        SetIfMissing(Groq, NeuralEntities.Chat, DefaultGroqChat, null);
        SetIfMissing(Groq, NeuralEntities.Ask, DefaultGroqAsk, null);
        SetIfMissing(Groq, NeuralEntities.GoogleSearch, DefaultGroqGoogleSearch, null);
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

    private bool ApplyGeminiFallback(string entity, string preferred, string alternate)
    {
        if (!Gemini.TryGetValue(entity, out var choice) || choice == null || choice.FallbackSpecified)
            return false;
        if (string.IsNullOrWhiteSpace(choice.Model))
            return false;

        var fallback = !string.Equals(choice.Model, preferred, StringComparison.OrdinalIgnoreCase)
            ? preferred
            : alternate;
        if (string.Equals(choice.Model, fallback, StringComparison.OrdinalIgnoreCase))
            return false;

        choice.Fallback = fallback;
        choice.FallbackSpecified = true;
        return true;
    }

    private static void SetIfMissing(Dictionary<string, NeuralModelChoice> map, string key, string model, string? fallback)
    {
        if (map.TryGetValue(key, out var existing) && existing != null && !string.IsNullOrWhiteSpace(existing.Model))
            return;
        map[key] = new NeuralModelChoice
        {
            Model = model,
            Fallback = fallback,
            FallbackSpecified = fallback != null
        };
    }
}

/// <summary>
/// Основная модель сущности и необязательная замена.
/// В старом <c>neural_models.json</c> на этом месте была одна строка.
/// </summary>
[JsonConverter(typeof(NeuralModelChoiceConverter))]
public class NeuralModelChoice
{
    /// <summary>
    /// В файле было поле <c>fallback</c>, в том числе пустое. Пустое значит «замену не пробовать».
    /// </summary>
    [JsonIgnore]
    public bool FallbackSpecified { get; set; }
    public bool HasDistinctFallback =>
        FallbackSpecified
        && !string.IsNullOrWhiteSpace(Fallback)
        && !string.Equals(Fallback.Trim(), Model?.Trim(), StringComparison.OrdinalIgnoreCase);
    public string Model { get; set; } = "";
    public string? Fallback { get; set; }
}

public sealed class NeuralModelChoiceConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(NeuralModelChoice);
    }

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
            return null;
        if (reader.TokenType == JsonToken.String)
            return new NeuralModelChoice {Model = reader.Value?.ToString()?.Trim() ?? ""};

        var obj = JObject.Load(reader);
        var fallbackToken = obj["fallback"];
        return new NeuralModelChoice
        {
            Model = (obj["model"] ?? obj["primary"])?.ToString()?.Trim() ?? "",
            Fallback = fallbackToken?.Type == JTokenType.Null ? "" : fallbackToken?.ToString()?.Trim(),
            FallbackSpecified = fallbackToken != null
        };
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        var choice = (NeuralModelChoice) value!;
        writer.WriteStartObject();
        writer.WritePropertyName("model");
        writer.WriteValue(choice.Model);
        if (choice.FallbackSpecified)
        {
            writer.WritePropertyName("fallback");
            writer.WriteValue(choice.Fallback ?? "");
        }

        writer.WriteEndObject();
    }
}

/// <summary>
/// Пробует основную модель, затем замену, если первая не вернула текст.
/// </summary>
public static class NeuralModelAttempts
{
    public static async Task<string?> RunAsync(IReadOnlyList<string> models, string entity, Func<string, Task<string?>> call)
    {
        models = GeminiDemandCooldown.PreferAvailable(models);
        for (var i = 0; i < models.Count; i++)
        {
            var text = await call(models[i]).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(text))
                return text;
            if (i + 1 < models.Count)
                Log.Warning("Модель {Model} для {Entity} не ответила, пробую замену {Fallback}.",
                    models[i], entity, models[i + 1]);
        }

        return null;
    }
}