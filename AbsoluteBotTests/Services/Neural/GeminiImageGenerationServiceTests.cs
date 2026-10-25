using System.Text;
using AbsoluteBot.Services.NeuralNetworkServices;
using AbsoluteBot.Services.UtilityServices;

namespace AbsoluteBotTests.Services.Neural;

[Trait("Category", "Unit")]
[Trait("Area", "Services")]
public class GeminiImageGenerationServiceTests
{
    [Fact]
    public async Task ProcessImageStreamAsync_ReadsInlineImage()
    {
        const string json =
            """{"candidates":[{"content":{"parts":[{"text":"ok"},{"inline_data":{"mime_type":"image/png","data":"ZmFrZQ=="}}]}}]}""";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

#pragma warning disable CS0618
        var service = new GeminiImageGenerationService(
            new GeminiSettingsProvider(new ConfigService(), new HttpClient()),
            new NeuralModelConfigService(),
            new HttpClient());
#pragma warning restore CS0618

        var (text, image) = await service.ProcessImageStreamAsync(stream);

        Assert.Equal("ok", text);
        Assert.Equal("ZmFrZQ==", image);
    }
}
