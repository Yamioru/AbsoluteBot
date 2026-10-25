using Serilog;
using Telegram.Bot;

namespace AbsoluteBot.Services.ChatServices.TelegramChat;

/// <summary>
///     Класс для удаления сообщений в Telegram чатах, отвечающий за удаление одного или нескольких сообщений
///     с учетом ограничений на количество удаляемых сообщений.
/// </summary>
public static class TelegramMessageDeletionHandler
{
    private const int MaxAttempts = 100;

    /// <summary>
    ///     Удаляет сообщения в чате Telegram.
    /// </summary>
    /// <param name="botClient">Клиент Telegram.</param>
    /// <param name="channelId">Идентификатор канала Telegram.</param>
    /// <param name="messageId">Идентификатор последнего сообщения для удаления.</param>
    /// <param name="count">Количество сообщений для удаления.</param>
    /// <returns>Задача, представляющая выполнение операции удаления сообщений.</returns>
    public static async Task DeleteMessagesAsync(ITelegramBotClient botClient, long channelId, int messageId, int count)
    {
        try
        {
            var deletedMessages = 0;
            var deleteMessageId = messageId - 1;
            var attempts = 0;

            while (ShouldContinueDeleting(deletedMessages, attempts, count))
            {
                var deleted = await TryDeleteMessageAsync(botClient, channelId, deleteMessageId).ConfigureAwait(false);

                if (deleted) deletedMessages++;

                deleteMessageId--;
                attempts++;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при удалении сообщений в Telegram.");
        }
    }

    /// <summary>
    ///     Проверяет, следует ли продолжать процесс удаления сообщений.
    /// </summary>
    /// <param name="deletedMessages">Количество удаленных сообщений.</param>
    /// <param name="attempts">Количество попыток удаления.</param>
    /// <param name="count">Требуемое количество сообщений для удаления.</param>
    /// <returns>True, если необходимо продолжить удаление сообщений, иначе False.</returns>
    private static bool ShouldContinueDeleting(int deletedMessages, int attempts, int count)
    {
        return deletedMessages < count && attempts < MaxAttempts;
    }

    /// <summary>
    ///     Пытается удалить сообщение из чата Telegram.
    /// </summary>
    /// <param name="botClient">Клиент Telegram.</param>
    /// <param name="channelId">Идентификатор канала Telegram.</param>
    /// <param name="messageId">Идентификатор сообщения.</param>
    /// <returns>True, если сообщение успешно удалено, иначе False.</returns>
    private static async Task<bool> TryDeleteMessageAsync(ITelegramBotClient botClient, long channelId, int messageId)
    {
        try
        {
            await botClient.DeleteMessageAsync(channelId, messageId).ConfigureAwait(false);
            return true;
        }
        catch
        {
            Log.Warning($"Не удалось удалить сообщение {messageId} в чате {channelId}. Продолжаем...");
            return false;
        }
    }
}