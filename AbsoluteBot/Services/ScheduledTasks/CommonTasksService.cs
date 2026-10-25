using AbsoluteBot.Services.NeuralNetworkServices;
using AbsoluteBot.Services.UtilityServices;
using Serilog;

namespace AbsoluteBot.Services.ScheduledTasks;

public class CommonTasksService(INeuralAskService neuralAsk, ConfigService configService)
{
    /// <summary>
    /// Выполняет ежедневные задачи в Telegram, такие как отправка информации о праздниках и курсе валют.
    /// </summary>
    public async Task ExecuteDailyCommonTask()
    {
        try
        {
            await NewsHandler().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при выполнении общих ежедневных задач.");
        }
    }

    /// <summary>
    /// Отправляет график курса валют в Telegram.
    /// </summary>
    private async Task NewsHandler()
    {
        const string prompt =
            "Загугли пожалуйста самые популярные новости за последние 3 дня на dtf.ru. Тема игры, кино и всё связанное с этим. В выводе пожалуйста напиши только сами новости в одну строку, не нужно водных слов. Новостей 5-7 штук. Если не удастся выполнить запрос, то напиши \"Ошибка\"";
        var news = await neuralAsk.AskAsync(prompt, 1000);
        if (!string.IsNullOrEmpty(news) && !news.Contains("Ошибка"))
            await configService.SetConfigValueAsync("News", news);
    }
}