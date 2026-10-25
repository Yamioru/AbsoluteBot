using AbsoluteBot.Services.NeuralNetworkServices;
using AbsoluteBotTests.Infrastructure;

namespace AbsoluteBotTests.Services.Neural;

[Collection("BeginData")]
[Trait("Category", "Unit")]
[Trait("Area", "Services")]
public class NeuralModelConfigServiceTests(BeginDataFixture fixture)
{
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
    public void Normalize_RejectsUnknownValues()
    {
        Assert.Null(NeuralModelConfigService.NormalizeProvider("openai"));
        Assert.Null(NeuralModelConfigService.NormalizeEntity("Foo"));
        Assert.Equal(NeuralEntities.GoogleSearch, NeuralModelConfigService.NormalizeEntity("googlesearch"));
        Assert.Null(NeuralModelConfigService.NormalizeEntity("achievements"));
    }
}
