using AbsoluteBot.Models;
using AbsoluteBot.Services.UserManagementServices;
using AbsoluteBotTests.Infrastructure;

namespace AbsoluteBotTests.Foundation;

[Collection("BeginData")]
[Trait("Category", "Unit")]
[Trait("Area", "Foundation")]
public class RoleServiceTests
{
    [Fact]
    public async Task Initialize_LoadsRolesFromTemp()
    {
        var service = new RoleService();
        await service.InitializeAsync();

        var role = await service.GetUserRoleAsync("cbet");
        Assert.Equal(UserRole.Administrator, role);
    }

    [Fact]
    public async Task GetUserRoleAsync_UnknownUser_ReturnsDefault()
    {
        var service = new RoleService();
        await service.InitializeAsync();

        var username = $"unknown_user_{Guid.NewGuid():N}";
        var role = await service.GetUserRoleAsync(username);

        Assert.Equal(UserRole.Default, role);
    }

    [Fact]
    public async Task SetUserRoleAsync_Ignored_Succeeds()
    {
        var service = new RoleService();
        await service.InitializeAsync();

        var username = $"ignored_user_{Guid.NewGuid():N}";
        Assert.True(await service.SetUserRoleAsync(username, UserRole.Ignored));
        Assert.Equal(UserRole.Ignored, await service.GetUserRoleAsync(username));
    }

    [Fact]
    public async Task SetUserRoleAsync_CannotDemoteAdministrator()
    {
        var service = new RoleService();
        await service.InitializeAsync();

        Assert.False(await service.SetUserRoleAsync("cbet", UserRole.Ignored));
        Assert.Equal(UserRole.Administrator, await service.GetUserRoleAsync("cbet"));
    }

    [Fact]
    public async Task GetUserRoleAsync_FollowsUserId_AfterNicknameChange()
    {
        var identity = new UserIdentityService();
        await identity.InitializeAsync();
        var service = new RoleService(identity);
        await service.InitializeAsync();

        var token = Guid.NewGuid().ToString("N")[..8];
        var userId = "id" + token;
        var oldNick = "role_old_" + token;
        var newNick = "role_new_" + token;

        Assert.True(await service.SetUserRoleAsync(oldNick, UserRole.Ignored, "VkPlayLive", userId));
        Assert.Equal(UserRole.Ignored, await service.GetUserRoleAsync(newNick, "VkPlayLive", userId));
        Assert.Equal(UserRole.Ignored, service.GetExistingUserRole(newNick, "VkPlayLive", userId));
    }
}
