using AbsoluteBot.Chat.Context;
using AbsoluteBot.Services.ChatServices.TelegramChat;
using AbsoluteBot.Services.UserManagementServices;
using Serilog;

namespace AbsoluteBot.Services.ScheduledTasks;

/// <summary>
///     Сервис для отправки уведомлений о днях рождения пользователей в Telegram.
/// </summary>
public class BirthdayTelegramNotificationService(BirthdayService birthdayService, TelegramChannelManager telegramChannelManager,
    TelegramChatService telegramChatService)
{
    private const string PlatformName = "Telegram";

    /// <summary>
    ///     Выполняет ежедневную проверку пользователей на наличие дня рождения и отправляет поздравления в Telegram.
    /// </summary>
    public async Task ExecuteDailyBirthdayCheck()
    {
        try
        {
            var birthdaysUsersNicknames = birthdayService.GetTodayBirthdaysNicknames();
            // Проверка на наличие хотя бы одного пользователя, чтобы избежать лишних вызовов
            if (birthdaysUsersNicknames.Count != 0)
            {
                var channelId = await telegramChannelManager.GetTelegramChannelId(ChannelType.Premium).ConfigureAwait(false);
                foreach (var userNicknames in birthdaysUsersNicknames)
                foreach (var nickname in userNicknames)
                {
                    var congratulation = await birthdayService.FindAndCongratulateUser(nickname, PlatformName).ConfigureAwait(false);
                    if (string.IsNullOrEmpty(congratulation)) continue;
                    await telegramChatService.SendMessageToChannelAsync(congratulation, channelId.ToString()).ConfigureAwait(false);
                    break; // Остановка после первого поздравления
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при ежедневной проверке дней рождения.");
        }
    }
}