using Serilog;

namespace AbsoluteBot.Services.UtilityServices;

/// <summary>
///     Прод-реализация: пишет <c>update.requested</c> в data-root (в Docker это volume <c>./data</c>).
/// </summary>
public class BotUpdateService : IBotUpdateService
{
    public const string RequestFileName = "update.requested";

    public Task RequestUpdateAsync()
    {
        var path = DataPaths.Get(RequestFileName);
        File.WriteAllText(path, DateTime.UtcNow.ToString("O"));
        Log.Information("Запрошено обновление бота, флаг {Path}", path);
        return Task.CompletedTask;
    }
}
