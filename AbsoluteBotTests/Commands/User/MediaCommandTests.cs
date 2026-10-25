using AbsoluteBot.Chat.Commands.UserCommands;
using AbsoluteBot.Models;
using AbsoluteBot.Services;
using AbsoluteBot.Services.MediaServices;
using AbsoluteBotTests.Infrastructure;
using Moq;

namespace AbsoluteBotTests.Commands.User;

[Trait("Category", "Unit")]
[Trait("Area", "Commands")]
public class MediaCommandTests
{
    [Theory]
    [InlineData(UserRole.Default, true)]
    [InlineData(UserRole.Ignored, false)]
    public void Image_CanExecute_ByRole(UserRole role, bool expected)
    {
        var cmd = new ImageCommand(new Mock<ImageSearchService>(new HttpClient()).Object);
        Assert.Equal(expected, cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name, role: role)));
    }

    [Fact]
    public async Task Image_MissingParameters_ReturnsUsage()
    {
        var cmd = new ImageCommand(new Mock<ImageSearchService>(new HttpClient()).Object);
        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name, ""));
        Assert.Contains("Использование", result);
    }

    [Fact]
    public async Task Image_HappyPath()
    {
        var images = new Mock<ImageSearchService>(new HttpClient());
        images.Setup(i => i.SearchImageAsync("кот")).ReturnsAsync("https://img/cat.png");
        var chat = new FakeChatService();
        var cmd = new ImageCommand(images.Object);

        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name, "кот", chat));

        Assert.Equal("https://img/cat.png", result);
        Assert.Single(chat.SentMessages);
    }

    [Fact]
    public async Task MultipleImage_MissingParameters_ReturnsUsage()
    {
        var cmd = new MultipleImageCommand(new Mock<ImageSearchService>(new HttpClient()).Object);
        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name, ""));
        Assert.Contains("Использование", result);
    }

    [Theory]
    [InlineData(UserRole.Default, true)]
    [InlineData(UserRole.Ignored, false)]
    public void MultipleImage_CanExecute_ByRole(UserRole role, bool expected)
    {
        var cmd = new MultipleImageCommand(new Mock<ImageSearchService>(new HttpClient()).Object);
        Assert.Equal(expected, cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name, role: role)));
    }

    [Fact]
    public async Task Gif_MissingParameters_ReturnsUsage()
    {
        var cmd = new GifCommand(new Mock<GifSearchService>(new HttpClient()).Object);
        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name, ""));
        Assert.Contains("Использование", result);
    }

    [Fact]
    public async Task Gif_HappyPath()
    {
        var gifs = new Mock<GifSearchService>(new HttpClient());
        gifs.Setup(g => g.SearchGifAsync("смех")).ReturnsAsync("https://gif.gif");
        var chat = new FakeChatService();
        var cmd = new GifCommand(gifs.Object);

        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name, "смех", chat));

        Assert.Equal("https://gif.gif", result);
    }

    [Theory]
    [InlineData(UserRole.Default, true)]
    [InlineData(UserRole.Ignored, false)]
    public void Cat_CanExecute_ByRole(UserRole role, bool expected)
    {
        var cmd = new CatCommand(new Mock<CatImageService>(new HttpClient()).Object);
        Assert.Equal(expected, cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name, role: role)));
    }

    [Fact]
    public async Task Cat_HappyPath()
    {
        var cats = new Mock<CatImageService>(new HttpClient());
        cats.Setup(c => c.GetCatImageAsync()).ReturnsAsync("https://cat.png");
        var chat = new FakeChatService();
        var cmd = new CatCommand(cats.Object);

        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name, chatService: chat));

        Assert.Equal("https://cat.png", result);
    }

    [Fact]
    public async Task Likeness_MissingParameters_ReturnsUsage()
    {
        var cmd = new LikenessCommand(new Mock<ImageSearchService>(new HttpClient()).Object);
        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name, ""));
        Assert.Contains("Использование", result);
    }

    [Fact]
    public async Task Likeness_HappyPath()
    {
        var images = new Mock<ImageSearchService>(new HttpClient());
        images.Setup(i => i.SearchImageAsync(It.Is<string>(s => s.Contains("Путин")))).ReturnsAsync("https://like.png");
        var chat = new FakeChatService();
        var cmd = new LikenessCommand(images.Object);

        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name, "Путин", chat));

        Assert.Equal("https://like.png", result);
    }

    [Fact]
    public async Task Video_MissingParameters_ReturnsUsage()
    {
        var cmd = new VideoCommand(new Mock<VideoSearchService>(new HttpClient()).Object);
        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name, ""));
        Assert.Contains("Использование", result);
    }

    [Fact]
    public async Task Video_HappyPath()
    {
        var videos = new Mock<VideoSearchService>(new HttpClient());
        videos.Setup(v => v.SearchVideoAsync("музыка")).ReturnsAsync("https://youtu.be/abc");
        var cmd = new VideoCommand(videos.Object);

        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateParsed(cmd.Name, "музыка"));

        Assert.Equal("https://youtu.be/abc", result);
    }

    [Fact]
    public void SpoilerImage_CanExecute_AdminOrPremium()
    {
        var cmd = new SpoilerImageCommand(new Mock<ImageSearchService>(new HttpClient()).Object);
        Assert.True(cmd.CanExecute(ChatContextFactory.CreateAdminChannelParsed(cmd.Name)));
        Assert.False(cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name)));
    }

    [Fact]
    public async Task SpoilerImage_MissingParameters_ReturnsUsage()
    {
        var cmd = new SpoilerImageCommand(new Mock<ImageSearchService>(new HttpClient()).Object);
        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateAdminChannelParsed(cmd.Name, ""));
        Assert.Contains("Использование", result);
    }

    [Fact]
    public void MultipleSpoiler_CanExecute_AdminOrPremium()
    {
        var cmd = new MultipleSpoilerImageCommand(new Mock<ImageSearchService>(new HttpClient()).Object);
        Assert.True(cmd.CanExecute(ChatContextFactory.CreateAdminChannelParsed(cmd.Name)));
        Assert.False(cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name)));
    }

    [Fact]
    public async Task MultipleSpoiler_MissingParameters_ReturnsUsage()
    {
        var cmd = new MultipleSpoilerImageCommand(new Mock<ImageSearchService>(new HttpClient()).Object);
        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateAdminChannelParsed(cmd.Name, ""));
        Assert.Contains("Использование", result);
    }
}
