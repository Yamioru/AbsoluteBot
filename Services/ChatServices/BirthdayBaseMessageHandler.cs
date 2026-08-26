using AbsoluteBot.Chat.Context;
using AbsoluteBot.Services.UserManagementServices;
using AbsoluteBot.Services.UtilityServices;

namespace AbsoluteBot.Services.ChatServices;

public abstract class BirthdayBaseMessageHandler(ConfigService configService, BirthdayService birthdayService, RoleService roleService) : BaseMessageHandler(configService, roleService)
{
    /// <summary>
    ///     Асинхронно отправляет поздравление с днем рождения, если сегодня день рождения пользователя.
    /// </summary>
    /// <param name="context">Контекст чата, в котором происходит взаимодействие.</param>
    /// <param name="username">Имя пользователя, которого нужно поздравить.</param>
    protected async Task BirthdayHandle(ChatContext context, string username)
    {
        var birthdayMessage = await birthdayService.FindAndCongratulateUser(username, context.Platform, context.UserId).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(birthdayMessage)) await context.ChatService.SendMessageAsync(birthdayMessage, context).ConfigureAwait(false);
    }
}