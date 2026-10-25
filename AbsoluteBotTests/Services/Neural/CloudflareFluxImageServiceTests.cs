using System.Net;
using AbsoluteBot.Services.NeuralNetworkServices;
using AbsoluteBot.Services.UtilityServices;
using AbsoluteBotTests.Infrastructure;
using Moq;

namespace AbsoluteBotTests.Services.Neural;

[Trait("Category", "Unit")]
[Trait("Area", "Services")]
public class CloudflareFluxImageServiceTests
{
    private const string TinyPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    [Fact]
    public async Task Generate_PostsMultipartPromptToFlux2Klein4b()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, """{"result":{"image":"ZmFrZQ=="}}""");
        var service = await CreateService(handler);

        var image = await service.GenerateOrEditAsync("a cat");

        Assert.Equal("ZmFrZQ==", image);
        Assert.Contains("flux-2-klein-4b", handler.LastRequestUri, StringComparison.Ordinal);
        Assert.Equal("Bearer cf-key", handler.LastAuthorization);
        Assert.Contains("prompt", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains("a cat", handler.LastBody, StringComparison.Ordinal);
        Assert.DoesNotContain("input_image_0", handler.LastBody, StringComparison.Ordinal);
        Assert.DoesNotContain("guidance", handler.LastBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Edit_IncludesInputImageAndEditPrompt()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, """{"result":{"image":"ZWRpdA=="}}""");
        var service = await CreateService(handler);

        var image = await service.GenerateOrEditAsync("make it blue", TinyPngBase64);

        Assert.Equal("ZWRpdA==", image);
        Assert.Contains("input_image_0", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains("make it blue", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains("image 0", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains("guidance", handler.LastBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Edit_RetriesWithSaferPrompt_OnFlaggedOutput()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.BadRequest,
            """{"errors":[{"message":"AiError: Your output has been flagged. Please choose another prompt / input image combination","code":3030}],"success":false}""");
        handler.EnqueueResponse(HttpStatusCode.OK, """{"result":{"image":"b2s="}}""");
        var service = await CreateService(handler);

        var image = await service.GenerateOrEditAsync("add a hat", TinyPngBase64);

        Assert.Equal("b2s=", image);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("Family-friendly", handler.LastBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generate_ReturnsNull_OnHttpError()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.BadRequest, """{"success":false}""");
        var service = await CreateService(handler);

        var image = await service.GenerateOrEditAsync("a cat");

        Assert.Null(image);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public void ComputeOutputSize_PreservesLandscapeAspect()
    {
        var (width, height) = CloudflareFluxImageService.ComputeOutputSize(1920, 1080);
        Assert.Equal(1024, width);
        Assert.True(height < width);
        Assert.Equal(0, height % 8);
    }

    [Fact]
    public void IsFlaggedError_DetectsCode3030()
    {
        Assert.True(CloudflareFluxImageService.IsFlaggedError(
            """{"errors":[{"message":"flagged","code":3030}],"success":false}"""));
        Assert.False(CloudflareFluxImageService.IsFlaggedError("""{"errors":[{"code":1000}],"success":false}"""));
    }

    private static async Task<CloudflareFluxImageService> CreateService(MockHttpMessageHandler handler)
    {
        var config = new Mock<ConfigService>();
        config.Setup(c => c.GetConfigValueAsync<string>("CloudFlareApiKey")).ReturnsAsync("cf-key");
        config.Setup(c => c.GetConfigValueAsync<string>("CloudFlareAccountId")).ReturnsAsync("acc-id");

        var service = new CloudflareFluxImageService(config.Object, new HttpClient(handler));
        await service.InitializeAsync();
        return service;
    }
}
