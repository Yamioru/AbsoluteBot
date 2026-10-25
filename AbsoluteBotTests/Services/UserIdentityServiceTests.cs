using AbsoluteBot.Services.GoogleSearch;
using AbsoluteBot.Services.UserManagementServices;
using AbsoluteBot.Services.UtilityServices;
using AbsoluteBotTests.Infrastructure;

namespace AbsoluteBotTests.Services;

[Collection("BeginData")]
[Trait("Category", "Unit")]
[Trait("Area", "Services")]
public class UserIdentityServiceTests
{
    [Fact]
    public async Task Remember_KeepsAliases_WhenNicknameChanges()
    {
        var identity = new UserIdentityService();
        await identity.InitializeAsync();
        var id = Guid.NewGuid().ToString("N")[..8];

        await identity.RememberAsync("old_nick_" + id, "VkPlayLive", "100" + id);
        var aliases = await identity.RememberAsync("new_nick_" + id, "VkPlayLive", "100" + id);

        Assert.Contains(aliases, a => a.Equals("old_nick_" + id, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(aliases, a => a.Equals("new_nick_" + id, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(aliases.Count, identity.GetAliases("new_nick_" + id, "VkPlayLive", "100" + id).Count);
    }

    [Fact]
    public async Task GetAliases_FindsOldNickname_ById()
    {
        var identity = new UserIdentityService();
        await identity.InitializeAsync();
        var id = Guid.NewGuid().ToString("N")[..8];

        await identity.RememberAsync("alice_" + id, "Twitch", "tw" + id);
        var aliases = identity.GetAliases("bob_" + id, "Twitch", "tw" + id);

        Assert.Contains(aliases, a => a.Equals("alice_" + id, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(aliases, a => a.Equals("bob_" + id, StringComparison.OrdinalIgnoreCase));
    }
}

[Collection("BeginData")]
[Trait("Category", "Unit")]
[Trait("Area", "Services")]
public class MbtiIdentityTests
{
    [Fact]
    public async Task GetMbti_FollowsUserId_AfterNicknameChange()
    {
        var identity = new UserIdentityService();
        await identity.InitializeAsync();
        var mbti = new MbtiService(new HttpClient(), new GoogleSearchService(new HttpClient(), new ConfigService()),
            identity);
        await mbti.InitializeAsync();

        var token = Guid.NewGuid().ToString("N")[..8];
        var userId = "vk" + token;
        var oldNick = "mbti_old_" + token;
        var newNick = "mbti_new_" + token;

        Assert.True(await mbti.SetMbtiForUserAsync(oldNick, "INTJ", "VkPlayLive", userId));
        Assert.Equal("INTJ", mbti.GetMbtiForUser(newNick, "VkPlayLive", userId));
        Assert.Equal("INTJ", mbti.GetMbtiForUser(oldNick));
    }
}
