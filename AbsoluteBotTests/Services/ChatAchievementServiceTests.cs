using System.Text.Json;
using AbsoluteBot.Models;
using AbsoluteBot.Services;
using AbsoluteBot.Services.NeuralNetworkServices;
using AbsoluteBot.Services.UserManagementServices;
using AbsoluteBotTests.Infrastructure;
using Moq;

namespace AbsoluteBotTests.Services;

[Collection("BeginData")]
[Trait("Category", "Unit")]
[Trait("Area", "Services")]
public class ChatAchievementServiceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ChatAchievementServiceTests(BeginDataFixture fixture)
    {
        _ = fixture;
        ChatAchievementService.Enabled = true;
    }

    [Fact]
    public async Task Initialize_LoadsThirtyAchievements()
    {
        var service = await CreateServiceAsync(_ => []);

        var catalog = service.GetCatalogSnapshot();
        Assert.Equal(30, catalog.Count);
        Assert.Contains(catalog, a => a.Id == "cat_lover");
        Assert.Contains(catalog, a => a.Id == "rain_man");
    }

    [Fact]
    public async Task TryRecord_CatLover_WritesProgressFile()
    {
        var service = await CreateServiceAsync(_ => ["cat_lover"]);

        await service.TryRecordAsync("vasya", "обожаю котиков", "Twitch");

        var snapshot = service.GetProgressSnapshot();
        var user = Assert.Single(snapshot, u => u.Nickname.Equals("vasya", StringComparison.OrdinalIgnoreCase));
        var entry = Assert.Single(user.Achievements);
        Assert.Equal("cat_lover", entry.Id);
        Assert.Equal(1, entry.Points);
        Assert.Equal(50, entry.Goal);
        Assert.False(entry.Unlocked);
        Assert.Equal("обожаю котиков", entry.LastMatch);

        var path = ChatAchievementService.GetProgressFilePath();
        Assert.True(File.Exists(path));
        var document = JsonSerializer.Deserialize<AchievementProgressDocument>(await File.ReadAllTextAsync(path), JsonOptions);
        Assert.NotNull(document);
        var fileUser = Assert.Single(document!.Users, u => u.Nickname.Equals("vasya", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, fileUser.Achievements[0].Points);
    }

    [Fact]
    public async Task TryRecord_SecondMessage_IncrementsWithoutCooldown()
    {
        var service = await CreateServiceAsync(_ => ["cat_lover"]);

        await service.TryRecordAsync("vasya", "котики милые", "Twitch");
        await service.TryRecordAsync("vasya", "ещё котики", "Twitch");

        Assert.Equal(2, ProgressPoints(service, "vasya", "cat_lover"));
    }

    [Fact]
    public async Task TryRecord_CommandAndEmpty_DoesNotCallClassifier()
    {
        var classifier = new Mock<IAchievementClassifier>(MockBehavior.Strict);
        var roles = new RoleService();
        await roles.InitializeAsync();
        var service = new ChatAchievementService(classifier.Object, roles);
        await service.InitializeAsync();

        await service.TryRecordAsync("vasya", "!команды", "Twitch");
        await service.TryRecordAsync("vasya", "   ", "Twitch");
        await service.TryRecordAsync("vasya", null, "Twitch");
        await service.TryRecordAsync(null, "привет", "Twitch");

        classifier.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TryRecord_UnknownId_IsIgnored()
    {
        var service = await CreateServiceAsync(_ => ["not_a_real_achievement", "cat_lover"]);

        await service.TryRecordAsync("vasya", "котики", "VkPlayLive");

        var entries = service.GetProgressSnapshot()
            .Single(u => u.Nickname.Equals("vasya", StringComparison.OrdinalIgnoreCase))
            .Achievements;
        Assert.Single(entries);
        Assert.Equal("cat_lover", entries[0].Id);
    }

    [Fact]
    public async Task TryRecord_FollowsUserId_AfterNicknameChange()
    {
        var identity = new UserIdentityService();
        await identity.InitializeAsync();
        var path = ChatAchievementService.GetProgressFilePath();
        if (File.Exists(path))
            File.Delete(path);

        var classifier = new Mock<IAchievementClassifier>();
        classifier.Setup(c => c.ClassifyAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<AchievementDefinition>>()))
            .ReturnsAsync(new[] {"cat_lover"});
        var roles = new RoleService();
        await roles.InitializeAsync();
        var clock = new TestTimeProvider(DateTimeOffset.UtcNow);
        var service = new ChatAchievementService(classifier.Object, roles, clock, identity);
        await service.InitializeAsync();

        var token = Guid.NewGuid().ToString("N")[..8];
        await service.TryRecordAsync("ach_old_" + token, "котики", "VkPlayLive", "uid" + token);
        await service.TryRecordAsync("ach_new_" + token, "ещё котики", "VkPlayLive", "uid" + token);

        var snapshot = service.GetProgressSnapshot();
        var user = Assert.Single(snapshot, u =>
            u.Nickname.Equals("ach_new_" + token, StringComparison.OrdinalIgnoreCase) ||
            u.Nickname.Equals("ach_old_" + token, StringComparison.OrdinalIgnoreCase));
        Assert.Equal("ach_new_" + token, user.Nickname);
        Assert.Equal(2, user.Achievements.Single(a => a.Id == "cat_lover").Points);
    }

    [Fact]
    public async Task TryRecord_IgnoredUser_DoesNotCallClassifier()
    {
        var classifier = new Mock<IAchievementClassifier>(MockBehavior.Strict);
        var roles = new RoleService();
        await roles.InitializeAsync();
        var ignored = "ignored_ach_" + Guid.NewGuid().ToString("N")[..8];
        Assert.True(await roles.SetUserRoleAsync(ignored, UserRole.Ignored));
        var service = new ChatAchievementService(classifier.Object, roles);
        await service.InitializeAsync();

        await service.TryRecordAsync(ignored, "обожаю котиков", "Twitch");

        classifier.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TryRecord_WritesCyrillicWithoutUnicodeEscapes()
    {
        var service = await CreateServiceAsync(_ => ["cat_lover"]);

        await service.TryRecordAsync("vasya", "котик", "Twitch");

        var json = await File.ReadAllTextAsync(ChatAchievementService.GetProgressFilePath());
        Assert.Contains("котик", json);
        Assert.DoesNotContain("\\u043A", json);
    }

    [Fact]
    public async Task Disabled_DoesNotLoadCatalogOrCallClassifier()
    {
        ChatAchievementService.Enabled = false;
        try
        {
            var classifier = new Mock<IAchievementClassifier>(MockBehavior.Strict);
            var roles = new RoleService();
            await roles.InitializeAsync();
            var service = new ChatAchievementService(classifier.Object, roles);
            await service.InitializeAsync();

            Assert.Empty(service.GetCatalogSnapshot());
            await service.TryRecordAsync("vasya", "котик", "Twitch");
            classifier.VerifyNoOtherCalls();
        }
        finally
        {
            ChatAchievementService.Enabled = true;
        }
    }

    private static async Task<ChatAchievementService> CreateServiceAsync(
        Func<string, IReadOnlyList<string>> classify, TimeProvider? time = null)
    {
        var path = ChatAchievementService.GetProgressFilePath();
        if (File.Exists(path))
            File.Delete(path);

        var classifier = new Mock<IAchievementClassifier>();
        classifier.Setup(c => c.ClassifyAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<AchievementDefinition>>()))
            .ReturnsAsync((string message, IReadOnlyList<AchievementDefinition> _) => classify(message));

        var roles = new RoleService();
        await roles.InitializeAsync();
        var service = time == null
            ? new ChatAchievementService(classifier.Object, roles)
            : new ChatAchievementService(classifier.Object, roles, time);
        await service.InitializeAsync();
        return service;
    }

    private static int ProgressPoints(ChatAchievementService service, string nickname, string id) =>
        service.GetProgressSnapshot()
            .Single(u => u.Nickname.Equals(nickname, StringComparison.OrdinalIgnoreCase))
            .Achievements.Single(a => a.Id == id).Points;

    private sealed class TestTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public TestTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan delta) => _utcNow += delta;
    }
}
