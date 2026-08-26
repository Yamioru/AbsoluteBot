using System.Collections.Concurrent;
using System.Text.Json;
using AbsoluteBot.Models;
using AbsoluteBot.Services.UtilityServices;
using Serilog;

namespace AbsoluteBot.Services.UserManagementServices;

/// <summary>
///     Сервис для управления ролями пользователей.
/// </summary>
public class RoleService : IAsyncInitializable
{
    private readonly UserIdentityService? userIdentityService;

    public RoleService() : this(null)
    {
    }

    public RoleService(UserIdentityService? userIdentityService)
    {
        this.userIdentityService = userIdentityService;
    }
    private static string FilePath => DataPaths.Get("user_roles.json");
    private static readonly SemaphoreSlim Semaphore = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private ConcurrentDictionary<string, UserRole> _userRoles = new();

    public async Task InitializeAsync()
    {
        await Semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            _userRoles = await LoadUserRolesAsync().ConfigureAwait(false);
        }
        finally
        {
            Semaphore.Release();
        }
    }

    public virtual Task<UserRole> GetUserRoleAsync(string username) =>
        GetUserRoleAsync(username, null, null);

    /// <summary>
    ///     Асинхронно возвращает роль пользователя по его имени и, если есть, по id на платформе.
    ///     Если пользователь не найден, присваивается роль по умолчанию.
    /// </summary>
    public virtual async Task<UserRole> GetUserRoleAsync(string username, string? platform, string? userId)
    {
        IReadOnlyList<string> aliases = Array.Empty<string>();
        try
        {
            if (userIdentityService != null)
                aliases = await userIdentityService.RememberAsync(username, platform, userId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при обновлении идентичности для роли.");
        }

        await Semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (NickKeyedStore.TryGet(_userRoles, WithCurrent(aliases, username), out var role))
            {
                var current = username.ToLower();
                if (!_userRoles.ContainsKey(current))
                {
                    _userRoles[current] = role;
                    await SaveUserRolesAsync(_userRoles).ConfigureAwait(false);
                }

                return role;
            }

            _userRoles[username.ToLower()] = UserRole.Default;
            await SaveUserRolesAsync(_userRoles).ConfigureAwait(false);

            return UserRole.Default;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при получении роли.");
            return UserRole.Default;
        }
        finally
        {
            Semaphore.Release();
        }
    }

    public virtual UserRole GetExistingUserRole(string username) =>
        GetExistingUserRole(username, null, null);

    /// <summary>
    ///     Возвращает уже известную роль без записи нового пользователя в файл.
    /// </summary>
    public virtual UserRole GetExistingUserRole(string username, string? platform, string? userId)
    {
        if (string.IsNullOrWhiteSpace(username)) return UserRole.Default;
        var aliases = userIdentityService?.GetAliases(username, platform, userId) ?? new[] {username};
        return NickKeyedStore.TryGet(_userRoles, WithCurrent(aliases, username), out var role) ? role : UserRole.Default;
    }

    /// <summary>
    ///     Асинхронно назначает новую роль пользователю.
    ///     Администраторы не могут быть понижены в правах.
    /// </summary>
    /// <param name="username">Имя пользователя.</param>
    /// <param name="role">Новая роль.</param>
    /// <returns>
    ///     <c>true</c>, если роль была успешно назначена;
    ///     <c>false</c>, если попытка изменить роль администратора или произошла ошибка.
    /// </returns>
    public virtual Task<bool> SetUserRoleAsync(string username, UserRole role) =>
        SetUserRoleAsync(username, role, null, null);

    public virtual async Task<bool> SetUserRoleAsync(string username, UserRole role, string? platform, string? userId)
    {
        IReadOnlyList<string> aliases = Array.Empty<string>();
        try
        {
            if (userIdentityService != null)
                aliases = await userIdentityService.RememberAsync(username, platform, userId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при обновлении идентичности для роли.");
        }

        await Semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            var keys = WithCurrent(aliases, username);
            if (NickKeyedStore.TryGet(_userRoles, keys, out var userRole) && userRole is UserRole.Administrator or UserRole.Bot)
                return false;

            NickKeyedStore.Set(_userRoles, keys, username, role);
            await SaveUserRolesAsync(_userRoles).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при задании роли пользователю.");
            return false;
        }
        finally
        {
            Semaphore.Release();
        }
    }

    private static IReadOnlyList<string> WithCurrent(IReadOnlyList<string> aliases, string username)
    {
        if (aliases.Count == 0) return new[] {username};
        if (aliases.Any(a => a.Equals(username, StringComparison.OrdinalIgnoreCase))) return aliases;
        var list = new List<string>(aliases.Count + 1) {username};
        list.AddRange(aliases);
        return list;
    }

    /// <summary>
    ///     Асинхронно загружает роли пользователей из файла.
    /// </summary>
    private static async Task<ConcurrentDictionary<string, UserRole>> LoadUserRolesAsync()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                Log.Warning("Не удалось загрузить список ролей пользователей, создание нового.");
                await SaveUserRolesAsync(new ConcurrentDictionary<string, UserRole>()).ConfigureAwait(false);
                return new ConcurrentDictionary<string, UserRole>();
            }

            var json = await File.ReadAllTextAsync(FilePath).ConfigureAwait(false);
            return JsonSerializer.Deserialize<ConcurrentDictionary<string, UserRole>>(json) ?? new ConcurrentDictionary<string, UserRole>();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Ошибка при загрузке ролей пользователей.");
            return new ConcurrentDictionary<string, UserRole>();
        }
    }

    /// <summary>
    ///     Асинхронно сохраняет роли пользователей в файл.
    /// </summary>
    private static async Task SaveUserRolesAsync(ConcurrentDictionary<string, UserRole> userRoles)
    {
        try
        {
            var json = JsonSerializer.Serialize(userRoles, JsonOptions);
            var tempFilePath = Path.GetTempFileName();

            await File.WriteAllTextAsync(tempFilePath, json).ConfigureAwait(false);
            await Task.Run(() => File.Move(tempFilePath, FilePath, true)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при сохранении ролей пользователей.");
        }
    }
}