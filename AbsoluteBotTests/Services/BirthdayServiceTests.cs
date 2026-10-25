using AbsoluteBot.Services.NeuralNetworkServices;
using AbsoluteBot.Services.UserManagementServices;
using AbsoluteBotTests.Infrastructure;
using Moq;

namespace AbsoluteBotTests.Services;

[Collection("BeginData")]
[Trait("Category", "Unit")]
[Trait("Area", "Services")]
public class BirthdayServiceTests
{
    private static BirthdayService CreateService()
    {
        var gemini = new Mock<INeuralAskService>();
        gemini.Setup(g => g.AskAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<double>()))
            .ReturnsAsync("поздравление");
        return new BirthdayService(gemini.Object);
    }

    [Fact]
    public async Task Initialize_LoadsWithoutThrowing()
    {
        var service = CreateService();
        await service.InitializeAsync();
        Assert.NotNull(service.GetAllBirthdays());
    }

    [Fact]
    public async Task AddOrUpdate_PersistsInTempRoot()
    {
        var service = CreateService();
        await service.InitializeAsync();
        var unique = "testuser_" + Guid.NewGuid().ToString("N")[..8];
        var added = await service.AddOrUpdateUserBirthday(unique, "WebText", new DateTime(2000, 1, 15));
        Assert.True(added);
        Assert.Contains(service.GetAllBirthdays(), b => b.UserName.Equals(unique, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AddOrUpdate_FollowsUserId_AfterNicknameChange()
    {
        var identity = new UserIdentityService();
        await identity.InitializeAsync();
        var gemini = new Mock<INeuralAskService>();
        gemini.Setup(g => g.AskAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<double>()))
            .ReturnsAsync("поздравление");
        var service = new BirthdayService(gemini.Object, identity);
        await service.InitializeAsync();

        var token = Guid.NewGuid().ToString("N")[..8];
        var userId = "bd" + token;
        var oldNick = "bd_old_" + token;
        var newNick = "bd_new_" + token;
        var date = new DateTime(2000, 3, 15);

        Assert.True(await service.AddOrUpdateUserBirthday(oldNick, "VkPlayLive", date, userId));
        var message = service.GetTimeUntilUserBirthdayForPlatform(newNick, "VkPlayLive", userId);
        Assert.DoesNotContain("не найдена", message);
        var info = service.GetAllBirthdays().Single(b =>
            b.PlatformIds.TryGetValue("VkPlayLive", out var id) && id == userId);
        Assert.Contains(info.Nicknames, n => n.Equals(newNick, StringComparison.OrdinalIgnoreCase));
    }
}
