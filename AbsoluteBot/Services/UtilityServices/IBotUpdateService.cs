namespace AbsoluteBot.Services.UtilityServices;

/// <summary>
///     Запрос обновления бота на хосте (git pull + docker compose).
///     Сам процесс в контейнере Docker не пересобирает: пишет флаг для host-watcher.
/// </summary>
public interface IBotUpdateService
{
    /// <summary>
    ///     Создаёт файл-флаг <c>update.requested</c> в корне данных.
    /// </summary>
    Task RequestUpdateAsync();
}
