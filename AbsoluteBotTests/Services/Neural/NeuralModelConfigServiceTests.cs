using AbsoluteBot.Services.NeuralNetworkServices;
using AbsoluteBotTests.Infrastructure;
using Newtonsoft.Json;

namespace AbsoluteBotTests.Services.Neural;

[Collection("BeginData")]
[Trait("Category", "Unit")]
[Trait("Area", "Services")]
public class NeuralModelConfigServiceTests(BeginDataFixture fixture)
{
    [Fact]
    public void ExplicitEmptyFallback_IsNotRefilled()
    {
        var document = JsonConvert.DeserializeObject<NeuralModelsDocument>("""
                                                                           {
                                                                             "gemini": {
                                                                               "Chat": { "model": "gemini-3.5-flash", "fallback": "" }
                                                                             }
                                                                           }
                                                                           """)!;

        Assert.False(document.ApplyMissingFallbacks());
        Assert.Equal("", document.Gemini[NeuralEntities.Chat].Fallback);
        Assert.False(document.Gemini[NeuralEntities.Chat].HasDistinctFallback);
    }

    [Fact]
    public void ExplicitPaidTier_StaysPaid()
    {
        var document = JsonConvert.DeserializeObject<NeuralModelsDocument>("""{ "tier": "paid" }""")!;
        document.EnsureDefaults();
        Assert.Equal(NeuralTiers.Paid, document.Tier);
    }

    [Fact]
    public async Task FormatForDisplay_ShowsCloudflareAsActiveDrawBackend()
    {
        _ = fixture;
        var service = new NeuralModelConfigService();
        await service.InitializeAsync();

        var display = service.FormatForDisplay();

        Assert.Contains("!нарисуй: Cloudflare", display, StringComparison.Ordinal);
        Assert.Contains(CloudflareFluxImageService.ModelId, display, StringComparison.Ordinal);
        Assert.Contains("запасной, не используется !нарисуй", display, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImageModel_AlwaysReadsGeminiSection()
    {
        _ = fixture;
        var service = new NeuralModelConfigService();
        await service.InitializeAsync();
        await service.SetProviderAsync(NeuralProviders.Groq);
        await service.SetModelAsync(NeuralEntities.Image, "gemini-3-pro-image-preview");
        Assert.Equal("gemini-3-pro-image-preview", service.GetModel(NeuralEntities.Image));
        await service.SetProviderAsync(NeuralProviders.Gemini);
    }

    [Fact]
    public async Task Initialize_LoadsDefaultsAndRoundTripsProviderAndModel()
    {
        _ = fixture;
        var service = new NeuralModelConfigService();
        await service.InitializeAsync();

        Assert.Equal(NeuralProviders.Gemini, service.GetProvider());
        Assert.False(string.IsNullOrWhiteSpace(service.GetModel(NeuralEntities.Ask)));

        Assert.True(await service.SetProviderAsync(NeuralProviders.Groq));
        Assert.Equal(NeuralProviders.Groq, service.GetProvider());

        Assert.True(await service.SetModelAsync(NeuralEntities.Ask, "openai/gpt-oss-120b"));
        Assert.Equal("openai/gpt-oss-120b", service.GetModel(NeuralEntities.Ask));

        Assert.True(await service.SetModelAsync(NeuralEntities.Chat, "gemini-3.5-flash", NeuralProviders.Gemini));
        await service.SetProviderAsync(NeuralProviders.Gemini);
        Assert.Equal("gemini-3.5-flash", service.GetModel(NeuralEntities.Chat));
        Assert.Equal(NeuralProviders.Gemini, service.GetProvider());

        await service.SetModelAsync(NeuralEntities.Chat, "gemini-3.8-flash", NeuralProviders.Gemini, "gemini-3.5-flash-lite", true);
    }

    [Fact]
    public void MissingTier_BecomesFree()
    {
        var document = JsonConvert.DeserializeObject<NeuralModelsDocument>("""{ "provider": "gemini" }""")!;
        Assert.Null(document.Tier);
        document.EnsureDefaults();
        Assert.Equal(NeuralTiers.Free, document.Tier);
    }

    [Fact]
    public void Normalize_RejectsUnknownValues()
    {
        Assert.Null(NeuralModelConfigService.NormalizeProvider("openai"));
        Assert.Null(NeuralModelConfigService.NormalizeEntity("Foo"));
        Assert.Equal(NeuralEntities.GoogleSearch, NeuralModelConfigService.NormalizeEntity("googlesearch"));
        Assert.Null(NeuralModelConfigService.NormalizeEntity("achievements"));
    }

    [Fact]
    public void OldStringFile_GainsGeminiFallback()
    {
        var document = JsonConvert.DeserializeObject<NeuralModelsDocument>("""
                                                                           {
                                                                             "provider": "gemini",
                                                                             "gemini": {
                                                                               "Chat": "gemini-3.5-flash",
                                                                               "Ask": "gemini-3.1-flash-lite",
                                                                               "GoogleSearch": "gemini-3.5-flash"
                                                                             }
                                                                           }
                                                                           """)!;

        Assert.False(document.Gemini[NeuralEntities.Chat].FallbackSpecified);
        Assert.True(document.ApplyMissingFallbacks());
        Assert.Equal("gemini-3.5-flash-lite", document.Gemini[NeuralEntities.Chat].Fallback);
        Assert.Equal("gemini-3.5-flash-lite", document.Gemini[NeuralEntities.Ask].Fallback);
        Assert.False(document.ApplyMissingFallbacks());
    }

    [Fact]
    public async Task SetModel_KeepsFallbackWhenOnlyPrimaryChanges()
    {
        _ = fixture;
        var service = new NeuralModelConfigService();
        await service.InitializeAsync();

        Assert.True(await service.SetModelAsync(
            NeuralEntities.Chat, "gemini-3.5-flash", NeuralProviders.Gemini, "gemini-3.1-flash-lite", true));
        Assert.True(await service.SetModelAsync(NeuralEntities.Chat, "gemini-3.6-flash", NeuralProviders.Gemini));

        Assert.Equal(new[] {"gemini-3.6-flash", "gemini-3.1-flash-lite"},
            service.GetAttemptModels(NeuralEntities.Chat, NeuralProviders.Gemini));

        await service.SetModelAsync(NeuralEntities.Chat, "gemini-3.8-flash", NeuralProviders.Gemini, "gemini-3.5-flash-lite", true);
    }
}