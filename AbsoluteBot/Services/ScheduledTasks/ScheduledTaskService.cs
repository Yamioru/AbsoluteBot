using Serilog;

namespace AbsoluteBot.Services.ScheduledTasks;

/// <summary>
///     Сервис для выполнения запланированных задач, таких как проверка стримов, отправка ежедневных уведомлений
///     и поздравлений с днем рождения.
/// </summary>
public class ScheduledTaskService(BirthdayTelegramNotificationService birthdayTelegramNotificationService,
    TelegramTasksService telegramTasksService, TwitchStreamMonitoringService twitchStreamMonitoringService, CommonTasksService commonTasksService) : IAsyncDisposable
{
    private const int TwitchCheckTimeMinutes = 1;
    private const int BirthdayCheckHour = 12;
    private const int CommonCheckHour = 18;
    private const int TelegramTaskHour = 11;
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public async ValueTask DisposeAsync()
    {
        await _cancellationTokenSource.CancelAsync().ConfigureAwait(false); // Отмена всех задач
        _cancellationTokenSource.Dispose(); // Освобождение ресурса
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Освобождает ресурсы, использованные данным экземпляром <see cref="ScheduledTaskService" />.
    /// </summary>
    /// <summary>
    ///     Запускает запланированные задачи: проверка стримов, поздравления с днем рождения и ежедневные задачи Telegram.
    /// </summary>
    public void Start()
    {
        _ = RunScheduledTasksAsync(_cancellationTokenSource.Token);
    }

    /// <summary>
    ///     Асинхронный запуск всех задач с таймерами.
    /// </summary>
    private async Task RunScheduledTasksAsync(CancellationToken cancellationToken)
    {
        var tasks = new[]
        {
            ScheduleDailyTask(BirthdayCheckHour, birthdayTelegramNotificationService.ExecuteDailyBirthdayCheck, cancellationToken),
            ScheduleDailyTask(TelegramTaskHour, telegramTasksService.ExecuteDailyTelegramTask, cancellationToken),
            ScheduleDailyTask(CommonCheckHour, commonTasksService.ExecuteDailyCommonTask, cancellationToken),
            SchedulePeriodicTask(TimeSpan.FromMinutes(TwitchCheckTimeMinutes),
                () => twitchStreamMonitoringService.ExecuteTwitchOnlineCheckTask(TwitchCheckTimeMinutes), cancellationToken)
        };

        await Task.WhenAll(tasks).ConfigureAwait(false); // Ожидание завершения всех задач
    }

    /// <summary>
    ///     Запланированная ежедневная задача для выполнения в заданный час дня.
    /// </summary>
    private static async Task ScheduleDailyTask(int hour, Func<Task> taskFunc, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var now = DateTime.Now;
            var nextRunTime = DateTime.Today.AddHours(hour);
            if (nextRunTime < now) nextRunTime = nextRunTime.AddDays(1); // Планирование на следующий день, если время прошло

            var delay = nextRunTime - now;

            try
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false); // Ожидание до времени выполнения
                await taskFunc().ConfigureAwait(false); // Выполнение задачи
            }
            catch (TaskCanceledException)
            {
                // Задача была отменена
                Log.Information("Ежедневная задача была отменена.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка выполнения ежедневной задачи.");
            }
        }
    }

    /// <summary>
    ///     Запланированная задача для выполнения через регулярные интервалы.
    /// </summary>
    private static async Task SchedulePeriodicTask(TimeSpan interval, Func<Task> taskFunc, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
            try
            {
                await Task.Delay(interval, cancellationToken).ConfigureAwait(false); // Ожидание интервала
                await taskFunc().ConfigureAwait(false); // Выполнение задачи
            }
            catch (TaskCanceledException)
            {
                // Задача была отменена
                Log.Information("Периодическая задача была отменена.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка выполнения периодической задачи.");
            }
    }
}