using System.Text.Json;
using AbsoluteBot.Models;
using AbsoluteBot.Services;
using AbsoluteBot.Services.UserManagementServices;
using AbsoluteBot.Services.UtilityServices;
using AbsoluteBotTests.Infrastructure;
using Moq;

namespace AbsoluteBotTests.Services;

[Collection("BeginData")]
[Trait("Category", "Unit")]
[Trait("Area", "Services")]
public class StreamChatterStatsServiceTests(BeginDataFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task RecordMessage_SumsSameNickname_AcrossCasing()
    {
        _ = fixture;
        var service = await CreateServiceAsync(801001);

        await service.RecordMessageAsync("alice");
        await service.RecordMessageAsync("Alice");
        await service.RecordMessageAsync("bob");

        var snapshot = service.GetChattersSnapshot();
        Assert.Equal(2, snapshot.Count);
        Assert.Equal("alice", snapshot[0].Nickname);
        Assert.Equal(2, snapshot[0].Count);
        Assert.Equal("bob", snapshot[1].Nickname);
        Assert.Equal(1, snapshot[1].Count);
    }

    [Fact]
    public async Task RecordMessage_WritesJsonFile()
    {
        _ = fixture;
        const int streamNumber = 801002;
        var service = await CreateServiceAsync(streamNumber);

        await service.RecordMessageAsync("carol");

        var path = StreamChatterStatsService.GetStatsFilePath(streamNumber);
        Assert.True(File.Exists(path));
        var document = JsonSerializer.Deserialize<StreamChatterStatsDocument>(await File.ReadAllTextAsync(path), JsonOptions);
        Assert.NotNull(document);
        Assert.Equal(streamNumber, document!.StreamNumber);
        Assert.Single(document.Chatters);
        Assert.Equal("carol", document.Chatters[0].Nickname);
        Assert.Equal(1, document.Chatters[0].Count);
    }

    [Fact]
    public async Task Initialize_ReloadsExistingFile()
    {
        _ = fixture;
        const int streamNumber = 801003;
        var first = await CreateServiceAsync(streamNumber);
        await first.RecordMessageAsync("dave");
        await first.RecordMessageAsync("dave");

        var restarted = await CreateServiceAsync(streamNumber);
        var snapshot = restarted.GetChattersSnapshot();

        Assert.Equal(streamNumber, restarted.CurrentStreamNumber);
        Assert.Single(snapshot);
        Assert.Equal("dave", snapshot[0].Nickname);
        Assert.Equal(2, snapshot[0].Count);
    }

    [Fact]
    public async Task RecordMessage_MergesCounts_WhenNicknameChangesWithSameId()
    {
        _ = fixture;
        var identity = new UserIdentityService();
        await identity.InitializeAsync();
        var config = new Mock<ConfigService>();
        config.Setup(c => c.GetConfigValueAsync<int>("StreamNumber")).ReturnsAsync(801010);
        var service = new StreamChatterStatsService(config.Object, identity);
        await service.InitializeAsync();

        var token = Guid.NewGuid().ToString("N")[..8];
        await service.RecordMessageAsync("stats_old_" + token, "VkPlayLive", "uid" + token);
        await service.RecordMessageAsync("stats_new_" + token, "VkPlayLive", "uid" + token);

        var snapshot = service.GetChattersSnapshot();
        Assert.Single(snapshot);
        Assert.Equal("stats_new_" + token, snapshot[0].Nickname);
        Assert.Equal(2, snapshot[0].Count);
    }

    [Fact]
    public async Task BeginNewStream_KeepsOldFile_AndResetsCounters()
    {
        _ = fixture;
        const int previousStream = 801004;
        const int nextStream = 801005;
        var service = await CreateServiceAsync(previousStream);
        await service.RecordMessageAsync("erin");

        await service.BeginNewStreamAsync(nextStream);

        Assert.Equal(nextStream, service.CurrentStreamNumber);
        Assert.Empty(service.GetChattersSnapshot());

        var oldDocument = JsonSerializer.Deserialize<StreamChatterStatsDocument>(
            await File.ReadAllTextAsync(StreamChatterStatsService.GetStatsFilePath(previousStream)), JsonOptions);
        Assert.Equal("erin", oldDocument!.Chatters.Single().Nickname);
        Assert.Equal(1, oldDocument.Chatters.Single().Count);

        var newPath = StreamChatterStatsService.GetStatsFilePath(nextStream);
        Assert.True(File.Exists(newPath));
        var newDocument = JsonSerializer.Deserialize<StreamChatterStatsDocument>(await File.ReadAllTextAsync(newPath), JsonOptions);
        Assert.Equal(nextStream, newDocument!.StreamNumber);
        Assert.Empty(newDocument.Chatters);
    }

    [Fact]
    public async Task RecordMessage_IgnoresEmptyNickname()
    {
        _ = fixture;
        var service = await CreateServiceAsync(801006);

        await service.RecordMessageAsync(" ");
        await service.RecordMessageAsync(null);

        Assert.Empty(service.GetChattersSnapshot());
    }

    [Fact]
    public async Task RecordMessage_WritesCyrillicNicknameUnescaped()
    {
        _ = fixture;
        const int streamNumber = 801007;
        var service = await CreateServiceAsync(streamNumber);

        await service.RecordMessageAsync("Ифа Вишес");
        await service.RecordMessageAsync("ифа вишес");
        await service.RecordMessageAsync("Хмелеслав Шмелевский");

        var snapshot = service.GetChattersSnapshot();
        Assert.Equal(2, snapshot.Count);
        Assert.Equal("Ифа Вишес", snapshot[0].Nickname);
        Assert.Equal(2, snapshot[0].Count);
        Assert.Equal("Хмелеслав Шмелевский", snapshot[1].Nickname);
        Assert.Equal(1, snapshot[1].Count);

        var json = await File.ReadAllTextAsync(StreamChatterStatsService.GetStatsFilePath(streamNumber));
        Assert.Contains("Ифа Вишес", json, StringComparison.Ordinal);
        Assert.Contains("Хмелеслав Шмелевский", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\\u0418", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Initialize_ReloadsCyrillicFromUnicodeEscapes()
    {
        _ = fixture;
        const int streamNumber = 801008;
        var path = StreamChatterStatsService.GetStatsFilePath(streamNumber);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, """
            {
              "streamNumber": 801008,
              "chatters": [
                {
                  "nickname": "\u0418\u0444\u0430 \u0412\u0438\u0448\u0435\u0441",
                  "count": 10
                }
              ]
            }
            """);

        var service = await CreateServiceAsync(streamNumber);
        var snapshot = service.GetChattersSnapshot();

        Assert.Single(snapshot);
        Assert.Equal("Ифа Вишес", snapshot[0].Nickname);
        Assert.Equal(10, snapshot[0].Count);

        var rewritten = await File.ReadAllTextAsync(path);
        Assert.Contains("Ифа Вишес", rewritten, StringComparison.Ordinal);
        Assert.DoesNotContain("\\u0418", rewritten, StringComparison.Ordinal);
    }

    private static async Task<StreamChatterStatsService> CreateServiceAsync(int streamNumber)
    {
        var config = new Mock<ConfigService>();
        config.Setup(c => c.GetConfigValueAsync<int>("StreamNumber")).ReturnsAsync(streamNumber);
        var service = new StreamChatterStatsService(config.Object);
        await service.InitializeAsync();
        return service;
    }
}
