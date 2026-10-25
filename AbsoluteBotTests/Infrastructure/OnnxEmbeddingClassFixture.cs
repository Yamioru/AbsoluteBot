using AbsoluteBot.Models;
using AbsoluteBot.Services.NeuralNetworkServices;
using AbsoluteBot.Services.UtilityServices;
using System.Text.Json;

namespace AbsoluteBotTests.Infrastructure;

/// <summary>
///     Одна ONNX-сессия e5-small на класс live-тестов. Предполагает, что BeginDataFixture уже выставил data-root.
/// </summary>
public sealed class OnnxEmbeddingClassFixture : IAsyncLifetime, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public OnnxEmbeddingAchievementClassifier Classifier { get; } =
        new(new HttpClient {Timeout = TimeSpan.FromMinutes(8)});

    public IReadOnlyList<AchievementDefinition> Catalog { get; private set; } =
        Array.Empty<AchievementDefinition>();

    public async Task InitializeAsync()
    {
        await Classifier.LoadModelAsync().ConfigureAwait(false);

        var path = DataPaths.Get("achievements.json");
        var json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
        var document = JsonSerializer.Deserialize<AchievementCatalogDocument>(json, JsonOptions);
        Catalog = document?.Achievements ?? new List<AchievementDefinition>();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => Classifier.Dispose();
}
